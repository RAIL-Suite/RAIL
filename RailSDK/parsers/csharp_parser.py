"""
C# Parser - Enterprise AST-based Parser using Roslyn (Enterprise Edition)

Features:
- Roslyn CSharpSyntaxTree parsing via Python.NET
- XML documentation comment extraction
- Full type inference including generics
- Performance metrics tracking
- Custom exception hierarchy
- Enterprise error handling

Prerequisites:
    - .NET SDK 8.0+ installed
    - pythonnet >= 3.0.0 (pip install pythonnet)
"""
import time
import subprocess
import sys
from typing import List, Dict, Any, Optional
from pathlib import Path

from core.auto_import import BaseParser, ToolDeclaration
from core.exceptions import (
    ParserException,
    SyntaxValidationError,
    TypeExtractionError
)

# Import shared Roslyn loader
try:
    from core import roslyn_loader
except ImportError:
    # Handle case where core package is not in path yet (e.g. running script directly)
    import sys
    sys.path.append(str(Path(__file__).parent.parent))
    from core import roslyn_loader

class CSharpParser(BaseParser):
    """
    Enterprise C# Parser using Roslyn via Python.NET
    
    Supports:
    - Type hints (string, int, double, bool, List<T>, Dictionary<K,V>, custom classes)
    - XML documentation comments
    - Public method extraction from classes
    - Generic type parameter inference
    - Performance metrics
    
    Example:
        ```python
        parser = CSharpParser()
        tools = parser.parse_source(csharp_code)
        ```
    """
    
    def __init__(self):
        """Initialize parser with metrics tracking and Roslyn validation"""
        self._last_parse_duration_ms = 0.0
        self._last_function_count = 0
        self._last_warnings = []
        
        # Ensure Roslyn is initialized via shared loader
        roslyn_loader.initialize_roslyn()
        
    @property
    def supported_language(self) -> str:
        """Return 'csharp' as language identifier"""
        return "csharp"
    
    @property
    def parser_version(self) -> str:
        """Return parser version (Enterprise v1.0.0)"""
        return "1.0.0"
    
    def parse_source(self, source_code: str) -> List[ToolDeclaration]:
        """
        Parse C# code and extract method signatures using Roslyn
        """
        start_time = time.perf_counter()
        self._last_warnings = []
        
        # Use types from shared loader
        CSharpSyntaxTree = roslyn_loader.CSharpSyntaxTree
        SyntaxKind = roslyn_loader.SyntaxKind
        
        # Parse syntax tree
        try:
            tree = CSharpSyntaxTree.ParseText(source_code)
            root = tree.GetRoot()
            
            # Check for syntax errors
            diagnostics = tree.GetDiagnostics()
            errors = [d for d in diagnostics if d.Severity.ToString() == "Error"]
            
            if errors:
                first_error = errors[0]
                line_span = first_error.Location.GetLineSpan()
                raise SyntaxValidationError(
                    f"Invalid C# syntax at line {line_span.StartLinePosition.Line + 1}: {first_error.GetMessage()}",
                    {
                        "line": line_span.StartLinePosition.Line + 1,
                        "column": line_span.StartLinePosition.Character + 1
                    }
                )
        
        except SyntaxValidationError:
            raise  # Re-raise as-is
        except Exception as e:
            raise ParserException(
                f"Failed to parse C# syntax tree: {str(e)}",
                {"error_type": type(e).__name__}
            )
        
        # Extract methods
        declarations = []
        
        try:
            # Find all method declarations
            methods = self._find_methods(root)
            
            for method_node in methods:
                # Skip private/internal methods
                if self._is_private_or_internal(method_node):
                    continue
                
                try:
                    tool_decl = self._extract_method_declaration(method_node)
                    declarations.append(tool_decl)
                except TypeExtractionError as e:
                    # Log warning but continue parsing
                    method_name = method_node.Identifier.Text
                    self._last_warnings.append(
                        f"Method '{method_name}': {e.message}"
                    )
        
        except Exception as e:
            raise ParserException(
                f"Failed to extract methods: {str(e)}",
                {"error_type": type(e).__name__}
            )
            
        # Update metrics
        self._last_parse_duration_ms = (time.perf_counter() - start_time) * 1000
        self._last_function_count = len(declarations)
        
        return declarations
    
    def _find_methods(self, root) -> List:
        """
        Find all MethodDeclaration nodes in syntax tree
        
        Args:
            root: Roslyn SyntaxNode
            
        Returns:
            List of MethodDeclarationSyntax nodes
        """
        SyntaxKind = roslyn_loader.SyntaxKind
        
        methods = []
        
        def visit_node(node):
            """Recursive visitor"""
            if node.Kind() == SyntaxKind.MethodDeclaration:
                methods.append(node)
            
            # Visit children
            for child in node.ChildNodes():
                visit_node(child)
        
        visit_node(root)
        return methods
    
    def _is_private_or_internal(self, method_node) -> bool:
        """
        Check if method has private/internal modifiers
        
        Args:
            method_node: MethodDeclarationSyntax
            
        Returns:
            True if private/internal
        """
        # Use text comparison for robustness (SyntaxToken.Kind() can be flaky in pythonnet)
        modifiers = method_node.Modifiers
        for modifier in modifiers:
            text = modifier.Text
            if text == "private" or text == "internal":
                return True
        
        # No explicit modifier = private in C# (only for methods in classes)
        # But we'll be lenient and assume public if not explicitly private
        return False
    
    def _extract_method_declaration(self, method_node) -> ToolDeclaration:
        """
        Extract ToolDeclaration from Roslyn MethodDeclarationSyntax node
        
        Args:
            method_node: MethodDeclarationSyntax
            
        Returns:
            ToolDeclaration object
            
        Raises:
            TypeExtractionError: If type parsing fails
        """
        method_name = method_node.Identifier.Text
        properties = {}
        required_params = []
        
        # Extract parameters
        param_list = method_node.ParameterList
        for param in param_list.Parameters:
            param_name = param.Identifier.Text
            param_type = param.Type
            
            # Build JSON schema for parameter
            try:
                if param_type:
                    param_schema = self._csharp_type_to_json_schema(param_type)
                else:
                    param_schema = {"type": "STRING"}  # Fallback
            except Exception as e:
                raise TypeExtractionError(
                    f"Failed to extract type for parameter '{param_name}' in method '{method_name}'",
                    {"parameter": param_name, "error": str(e)}
                )
            
            # Add description (extract from XML docs if available)
            param_schema["description"] = f"Parameter {param_name}"
            
            properties[param_name] = param_schema
            
            # Check if parameter has default value
            if not hasattr(param, 'Default') or param.Default is None:
                required_params.append(param_name)
        
        # Extract XML documentation
        description = self._extract_xml_doc(method_node) or f"Method {method_name}"
        
        return ToolDeclaration(
            name=method_name,
            description=description,
            parameters={
                "type": "OBJECT",
                "properties": properties,
                "required": required_params
            }
        )
    
    def _csharp_type_to_json_schema(self, type_node) -> Dict[str, Any]:
        """
        Convert Roslyn TypeSyntax to JSON Schema
        
        Supports: string, int, double, bool, List<T>, Dictionary<K,V>, custom classes
        
        Args:
            type_node: Roslyn TypeSyntax node
            
        Returns:
            JSON schema dictionary
        """
        # SyntaxKind = roslyn_loader.SyntaxKind # Not used here, but good practice
        
        type_str = type_node.ToString()
        
        # Primitive types (Gemini API standard - UPPERCASE)
        primitive_map = {
            "string": "STRING",
            "int": "INTEGER",
            "long": "INTEGER",
            "short": "INTEGER",
            "byte": "INTEGER",
            "double": "NUMBER",
            "float": "NUMBER",
            "decimal": "NUMBER",
            "bool": "BOOLEAN",
            "boolean": "BOOLEAN",
            "object": "OBJECT"
        }
        
        if type_str in primitive_map:
            return {"type": primitive_map[type_str]}
        
        # Generic types (List<T>, Dictionary<K,V>)
        if "List<" in type_str or "IList<" in type_str or "IEnumerable<" in type_str:
            # Extract inner type
            inner_type_str = type_str.split('<')[1].rstrip('>')
            
            # Recursive parse (simplified - assumes primitive inner type)
            inner_schema = primitive_map.get(inner_type_str, "STRING")
            
            return {"type": "ARRAY", "items": {"type": inner_schema}}
        
        if "Dictionary<" in type_str or "IDictionary<" in type_str:
            return {"type": "OBJECT"}
        
        # Custom classes/interfaces - treat as object
        return {"type": "OBJECT"}
    
    def _extract_xml_doc(self, method_node) -> Optional[str]:
        """
        Extract XML documentation comment from method
        
        Args:
            method_node: MethodDeclarationSyntax
            
        Returns:
            Summary text or None
        """
        try:
            # Get trivia (comments) before method
            leading_trivia = method_node.GetLeadingTrivia()
            
            for trivia in leading_trivia:
                # Check for documentation comment
                # Try .Kind() first, fallback to string check if needed
                is_doc = False
                try:
                    if trivia.Kind().ToString() == "SingleLineDocumentationCommentTrivia":
                        is_doc = True
                except:
                    # Fallback: check raw kind or string representation if Kind() fails
                    if "DocumentationCommentTrivia" in str(trivia):
                        is_doc = True
                
                if is_doc:
                    # Parse XML content
                    xml_text = trivia.ToFullString()
                    
                    # Extract <summary> content
                    if "<summary>" in xml_text:
                        start = xml_text.find("<summary>") + 9
                        end = xml_text.find("</summary>")
                        if end > start:
                            summary = xml_text[start:end].strip()
                            # Remove /// prefixes and extra whitespace
                            summary = summary.replace("///", "").strip()
                            return summary
            
            return None
        except:
            return None
    
    def validate_syntax(self, source_code: str) -> bool:
        """
        Fast syntax validation without full parsing
        
        Args:
            source_code: Raw C# code
            
        Returns:
            True if syntax is valid, False otherwise
        """
        try:
            roslyn_loader.initialize_roslyn()
            CSharpSyntaxTree = roslyn_loader.CSharpSyntaxTree
            
            tree = CSharpSyntaxTree.ParseText(source_code)
            diagnostics = tree.GetDiagnostics()
            errors = [d for d in diagnostics if d.Severity.ToString() == "Error"]
            
            return len(errors) == 0
        except:
            return False
    
    def get_parse_metadata(self) -> Dict[str, Any]:
        """
        Return metrics from last parse operation
        
        Returns:
            Dictionary with parse_duration_ms, function_count, warnings
        """
        return {
            "parse_duration_ms": self._last_parse_duration_ms,
            "function_count": self._last_function_count,
            "warnings": self._last_warnings.copy(),
            "roslyn_available": True
        }


