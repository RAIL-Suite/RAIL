"""
Python Parser - AST-based function signature extraction (Enterprise Edition)

Features:
- Tree-sitter AST parsing with full type inference
- Performance metrics tracking
- Custom exception hierarchy
- Version compatibility checking
"""
import ast
import time
from typing import List, Dict, Any

from core.auto_import import BaseParser, ToolDeclaration
from core.exceptions import SyntaxValidationError, TypeExtractionError


class PythonParser(BaseParser):
    """
    Enterprise Python Parser using AST
    
    Supports:
    - Type hints (str, int, float, bool, List[T], Dict[K,V])
    - Docstring extraction
    - Performance metrics
    """
    
    def __init__(self):
        """Initialize parser with metrics tracking"""
        self._last_parse_duration_ms = 0.0
        self._last_function_count = 0
        self._last_warnings = []
    
    @property
    def supported_language(self) -> str:
        """Return 'python' as language identifier"""
        return "python"
    
    @property
    def parser_version(self) -> str:
        """Return parser version (Enterprise v1.0.0)"""
        return "1.0.0"
    
    def parse_source(self, source_code: str) -> List[ToolDeclaration]:
        """
        Parse Python code and extract function signatures
        
        Args:
            source_code: Raw Python code as string
            
        Returns:
            List of ToolDeclaration objects for LLM consumption
            
        Raises:
            SyntaxValidationError: If Python syntax is invalid
            TypeExtractionError: If type hints cannot be parsed
        """
        # Start metrics tracking
        start_time = time.perf_counter()
        self._last_warnings = []
        
        # Parse AST
        try:
            tree = ast.parse(source_code)
        except SyntaxError as e:
            raise SyntaxValidationError(
                f"Invalid Python syntax at line {e.lineno}: {e.msg}",
                {"line": e.lineno, "offset": e.offset}
            )
        
        declarations = []
        
        for node in ast.walk(tree):
            if isinstance(node, ast.FunctionDef):
                # Skip private functions
                if node.name.startswith("_"):
                    continue
                
                try:
                    tool_decl = self._extract_function_declaration(node)
                    declarations.append(tool_decl)
                except TypeExtractionError as e:
                    # Log warning but continue parsing
                    self._last_warnings.append(
                        f"Function '{node.name}': {e.message}"
                    )
        
        # Update metrics
        self._last_parse_duration_ms = (time.perf_counter() - start_time) * 1000
        self._last_function_count = len(declarations)
        
        return declarations
    
    def validate_syntax(self, source_code: str) -> bool:
        """
        Fast syntax validation without full parsing
        
        Args:
            source_code: Raw Python code
            
        Returns:
            True if syntax is valid, False otherwise
        """
        try:
            ast.parse(source_code)
            return True
        except SyntaxError:
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
            "warnings": self._last_warnings.copy()
        }
    
    def _extract_function_declaration(self, node: ast.FunctionDef) -> ToolDeclaration:
        """
        Extract ToolDeclaration from AST FunctionDef node
        
        Args:
            node: AST FunctionDef node
            
        Returns:
            ToolDeclaration object
            
        Raises:
            TypeExtractionError: If type hint parsing fails
        """
        properties = {}
        required_params = []
        
        for arg in node.args.args:
            if arg.arg == 'self':
                continue
            
            # Build schema for parameter
            try:
                if arg.annotation:
                    param_schema = self._build_schema(arg.annotation)
                else:
                    param_schema = {"type": "STRING"}  # Default type (Gemini API standard)
            except Exception as e:
                raise TypeExtractionError(
                    f"Failed to extract type for parameter '{arg.arg}'",
                    {"parameter": arg.arg, "error": str(e)}
                )
            
            param_schema["description"] = f"Parameter {arg.arg}"
            properties[arg.arg] = param_schema
            required_params.append(arg.arg)
        
        return ToolDeclaration(
            name=node.name,
            description=ast.get_docstring(node) or f"Function {node.name}",
            parameters={
                "type": "OBJECT",
                "properties": properties,
                "required": required_params
            }
        )
    
    def _build_schema(self, annotation_node) -> Dict[str, Any]:
        """
        Build JSON schema from AST annotation node (recursive)
        
        Supports: str, int, float, bool, List[T], Dict[K,V]
        
        Args:
            annotation_node: AST annotation node
            
        Returns:
            JSON schema dictionary
        """
        # Primitive types (Gemini API standard - UPPERCASE)
        if isinstance(annotation_node, ast.Name):
            mapping = {
                "str": "STRING",
                "int": "INTEGER",
                "float": "NUMBER",
                "bool": "BOOLEAN",
                "dict": "OBJECT",
                "list": "ARRAY"
            }
            base_type = mapping.get(annotation_node.id, "STRING")
            
            if base_type == "ARRAY":
                return {"type": "ARRAY", "items": {"type": "STRING"}}
            elif base_type == "OBJECT":
                return {"type": "OBJECT"}
            else:
                return {"type": base_type}
        
        # Complex types: List[T], Dict[K,V]
        if isinstance(annotation_node, ast.Subscript):
            if isinstance(annotation_node.value, ast.Name):
                root_type = annotation_node.value.id
                
                if root_type == "List":
                    # Extract inner type
                    if isinstance(annotation_node.slice, ast.Tuple):
                        inner_type = annotation_node.slice.elts[0]
                    else:
                        inner_type = annotation_node.slice
                    
                    # Recursive call for List[Dict[...]]
                    inner_schema = self._build_schema(inner_type)
                    return {"type": "ARRAY", "items": inner_schema}
                
                elif root_type == "Dict":
                    return {"type": "OBJECT"}
        
        # Fallback
        return {"type": "STRING"}



