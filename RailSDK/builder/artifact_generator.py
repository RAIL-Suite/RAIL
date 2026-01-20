"""
Rail Builder - Artifact Generator
Core logic for analyzing source code and creating Rail Artifacts

This module bridges the Rail-bridge parsers with artifact packaging.
"""

import sys
import uuid
import shutil
from pathlib import Path
from typing import List, Optional
from datetime import datetime

# Add parent directory and Rail-bridge to path
parent_dir = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(parent_dir))
sys.path.insert(0, str(parent_dir / "Rail-bridge"))

from core.manifest import ManifestSchema, ArtifactMetadata
from core.schema import ToolDeclaration
from core.exceptions import ParserException, ArtifactException
from parsers import get_parser


class ArtifactGenerator:
    """
    Artifact Generator - Converts source code to Rail Artifacts
    
    Responsibilities:
    - Parse source code using language-specific parser
    - Extract dependencies (imports, packages)
    - Generate manifest.json
    - Package files into artifact directory
    """
    
    def __init__(self):
        """Initialize artifact generator"""
        self.version = "1.0.0"
    
    def build(
        self,
        source_path: str,
        output_dir: str = "./Rail_artifacts",
        artifact_name: Optional[str] = None
    ) -> Path:
        """
        Build Rail Artifact from source code
        
        Args:
            source_path: Path to source file (e.g., 'my_script.py')
            output_dir: Directory to save artifact (default: './Rail_artifacts')
            artifact_name: Custom artifact name (default: source filename)
            
        Returns:
            Path to created artifact directory
            
        Raises:
            ParserException: If source code parsing fails
            ArtifactException: If artifact creation fails
        """
        source_file = Path(source_path)
        
        # Determine artifact name
        if not artifact_name:
            artifact_name = source_file.stem
        
        # Create output directory
        output_path = Path(output_dir) / artifact_name
        output_path.mkdir(parents=True, exist_ok=True)
        
        print(f"Creating artifact: {artifact_name}")
        
        # Step 1: Parse source code
        print(f"Parsing source code...")
        tools = self._parse_source(source_file)
        print(f"Found {len(tools)} functions")
        
        # Step 2: Extract dependencies
        print(f"Analyzing dependencies...")
        dependencies = self._extract_dependencies(source_file)
        print(f"Found {len(dependencies)} external packages")
        
        # Step 3: Copy source file
        print(f" Copying source file...")
        entry_point_name = source_file.name
        shutil.copy(source_file, output_path / entry_point_name)
        print(f"Copied: {entry_point_name}")
        
        # Step 4: Generate manifest
        print(f"Generating manifest...")
        manifest = self._create_manifest(
            language=self._detect_language(source_file),
            entry_point=entry_point_name,
            tools=tools,
            dependencies=dependencies,
            source_file=source_file.name
        )
        
        manifest_path = output_path / "manifest.json"
        manifest.save_to_file(str(manifest_path))
        print(f"Created: manifest.json")
        
        # Step 5: Create README
        print(f"Generating README...")
        self._create_readme(output_path, manifest)
        print(f"Created: README.md")
        
        return output_path
    
    def _parse_source(self, source_file: Path) -> List[ToolDeclaration]:
        """
        Parse source code and extract tool declarations
        
        Args:
            source_file: Path to source file
            
        Returns:
            List of ToolDeclaration objects
            
        Raises:
            ParserException: If parsing fails
        """
        try:
            # Get appropriate parser
            parser = get_parser(source_file.name)
            
            # Read source code
            source_code = source_file.read_text(encoding='utf-8')
            
            # Parse
            tools = parser.parse_source(source_code)
            
            return tools
            
        except Exception as e:
            raise ParserException(
                f"Failed to parse {source_file.name}: {str(e)}",
                {"file": str(source_file), "error": str(e)}
            )
    
    def _extract_dependencies(self, source_file: Path) -> List[str]:
        """
        Extract external dependencies from source file
        
        Args:
            source_file: Path to source file
            
        Returns:
            List of dependency strings (e.g., ['pandas', 'numpy'])
            
        Note:
            For Python, this uses AST to find imports.
            Version pinning is NOT done here (users must manually specify in requirements.txt)
        """
        if source_file.suffix == '.cs':
            # For C#, look for sibling .csproj file
            csproj_files = list(source_file.parent.glob("*.csproj"))
            if csproj_files:
                try:
                    import xml.etree.ElementTree as ET
                    tree = ET.parse(csproj_files[0])
                    root = tree.getroot()
                    # Handle both new SDK style and old style
                    # Look for <PackageReference Include="...">
                    packages = []
                    for package in root.findall(".//PackageReference"):
                        name = package.get("Include")
                        version = package.get("Version")
                        if name:
                            packages.append(f"{name}=={version}" if version else name)
                    return sorted(packages)
                except Exception:
                    return []
            return []
        
        if source_file.suffix != '.py':
            return []
        
        import ast
        
        try:
            source_code = source_file.read_text(encoding='utf-8')
            tree = ast.parse(source_code)
            
            # Extract imports
            imports = set()
            for node in ast.walk(tree):
                if isinstance(node, ast.Import):
                    for alias in node.names:
                        imports.add(alias.name.split('.')[0])
                elif isinstance(node, ast.ImportFrom):
                    if node.module:
                        imports.add(node.module.split('.')[0])
            
            # Filter out stdlib (basic check)
            stdlib = {
                'os', 'sys', 'json', 'time', 'datetime', 're', 'math',
                'random', 'collections', 'itertools', 'functools', 'pathlib',
                'typing', 'abc', 'ast', 'inspect', 'importlib', 'subprocess'
            }
            
            external = sorted(imports - stdlib)
            return external
            
        except Exception:
            return []  # Best effort, don't fail build
    
    def _detect_language(self, source_file: Path) -> str:
        """
        Detect programming language from file extension
        
        Args:
            source_file: Path to source file
            
        Returns:
            Language identifier ('python', 'csharp', etc.)
        """
        mapping = {
            '.py': 'python',
            '.cs': 'csharp',
            '.js': 'javascript',
            '.ts': 'typescript'
        }
        return mapping.get(source_file.suffix, 'unknown')
    
    def _create_manifest(
        self,
        language: str,
        entry_point: str,
        tools: List[ToolDeclaration],
        dependencies: List[str],
        source_file: str
    ) -> ManifestSchema:
        """
        Create ManifestSchema object
        
        Args:
            language: Programming language
            entry_point: Entry point filename
            tools: List of tool declarations
            dependencies: List of dependencies
            source_file: Original source filename
            
        Returns:
            ManifestSchema instance
        """
        metadata = ArtifactMetadata(
            artifact_id=str(uuid.uuid4()),
            created_at=datetime.utcnow().isoformat(),
            source_file=source_file,
            build_info={
                "builder_version": self.version,
                "timestamp": datetime.utcnow().isoformat()
            }
        )
        
        manifest = ManifestSchema(
            version="1.0",
            language=language,
            entry_point=entry_point,
            tools=tools,
            dependencies=dependencies,
            metadata=metadata
        )
        
        return manifest
    
    def _create_readme(self, artifact_path: Path, manifest: ManifestSchema) -> None:
        """
        Generate README.md for the artifact
        
        Args:
            artifact_path: Path to artifact directory
            manifest: ManifestSchema object
        """
        readme_content = f"""# Rail Artifact: {artifact_path.name}

**Generated by Rail Builder v{self.version}**

## Metadata
- **Artifact ID**: `{manifest.metadata.artifact_id}`
- **Created**: {manifest.metadata.created_at}
- **Language**: {manifest.language}
- **Entry Point**: `{manifest.entry_point}`

## Available Functions

{self._format_tools_table(manifest.tools)}

## Dependencies

{self._format_dependencies(manifest.dependencies)}

## Usage

### With Rail Runtime (Python)

```python
from rail_runtime import railRuntime

# Load artifact
runtime = RailRuntime("{artifact_path}")
runtime.load()

# Execute function
result = runtime.execute("function_name", {{"param": "value"}})
print(result)
```

### With Rail Web

Upload the artifact directory (or zip it) to the Rail Web interface.

## Files

- `manifest.json` - Artifact metadata and tool definitions
- `{manifest.entry_point}` - Source code
- `README.md` - This file
"""
        
        readme_path = artifact_path / "README.md"
        readme_path.write_text(readme_content, encoding='utf-8')
    
    def _format_tools_table(self, tools: List[ToolDeclaration]) -> str:
        """Format tools as markdown table"""
        if not tools:
            return "_No functions found_"
        
        lines = ["| Function | Description |", "|----------|-------------|"]
        for tool in tools:
            lines.append(f"| `{tool.name}` | {tool.description} |")
        
        return "\n".join(lines)
    
    def _format_dependencies(self, dependencies: List[str]) -> str:
        """Format dependencies as markdown list"""
        if not dependencies:
            return "_No external dependencies_"
        
        lines = []
        for dep in dependencies:
            lines.append(f"- `{dep}`")
        
        return "\n".join(lines)


