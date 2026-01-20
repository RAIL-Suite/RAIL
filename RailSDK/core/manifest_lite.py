"""
Rail Bridge - Manifest Schema (Lightweight - Zero Dependencies)

This is the dataclass version of manifest.py for use in Builder and Runtime CLI.
Uses only Python stdlib (no Pydantic) for maximum portability and zero-install distribution.

The Manifest is the "contract" between Builder and Runtime:
- Builder generates manifest.json from source code
- Runtime loads manifest.json for instant parsing (no re-analysis)
- Ensures cross-product compatibility (Web, Builder, Runtime)

For Web/FastAPI usage with validation, see manifest.py (with Pydantic).
"""

from typing import List, Dict, Any, Optional
from datetime import datetime
from dataclasses import dataclass, field, asdict
import json
from pathlib import Path

from .schema_lite import ToolDeclarationLite


@dataclass
class ArtifactMetadataLite:
    """
    Metadata about the artifact (authorship, build info, etc.) - Lightweight version
    """
    artifact_id: str
    created_at: str = field(default_factory=lambda: datetime.utcnow().isoformat())
    author: Optional[str] = None
    description: Optional[str] = None
    source_file: Optional[str] = None
    build_info: Dict[str, Any] = field(default_factory=dict)
    tags: List[str] = field(default_factory=list)
    
    def dict(self) -> Dict[str, Any]:
        """Convert to dictionary (Pydantic-compatible API)"""
        return asdict(self)


@dataclass
class ManifestSchemaLite:
    """
    Standardized Rail Artifact Manifest (v1.0) - Lightweight version
    
    This is the core contract between Builder and Runtime.
    All fields are designed for forward/backward compatibility.
    
    Example:
        ```python
        manifest = ManifestSchemaLite(
            version="1.0",
            language="python",
            entry_point="source_logic.py",
            tools=[tool1, tool2],
            dependencies=["pandas==2.0.0"]
        )
        manifest.save_to_file("manifest.json")
        ```
    """
    
    # Core Fields (REQUIRED)
    version: str
    language: str
    entry_point: str
    tools: List[ToolDeclarationLite]
    
    # Optional Fields
    dependencies: List[str] = field(default_factory=list)
    metadata: Optional[ArtifactMetadataLite] = None
    runtime_requirements: Dict[str, Any] = field(default_factory=dict)
    security_policy: Dict[str, Any] = field(default_factory=dict)
    
    def __post_init__(self):
        """Validate fields after initialization"""
        # Validate version (SemVer format)
        parts = self.version.split('.')
        if len(parts) < 2 or not all(p.isdigit() for p in parts[:2]):
            raise ValueError(f"Invalid version format: {self.version}. Expected SemVer (e.g., '1.0' or '1.0.0')")
        
        # Validate language
        supported = ['python', 'csharp', 'javascript', 'typescript']
        if self.language.lower() not in supported:
            raise ValueError(
                f"Unsupported language: {self.language}. "
                f"Supported: {', '.join(supported)}"
            )
        self.language = self.language.lower()
        
        # Validate tools
        if not self.tools:
            raise ValueError("Manifest must contain at least one tool")
        
        # Ensure metadata exists
        if self.metadata is None:
            self.metadata = ArtifactMetadataLite(artifact_id="unknown")
    
    def dict(self, exclude_none: bool = True) -> Dict[str, Any]:
        """
        Convert to dictionary (Pydantic-compatible API)
        
        Args:
            exclude_none: If True, exclude None values
            
        Returns:
            Dictionary representation
        """
        data = asdict(self)
        if exclude_none:
            data = {k: v for k, v in data.items() if v is not None}
        return data
    
    def to_json_dict(self) -> Dict[str, Any]:
        """
        Convert to JSON-serializable dictionary
        
        Returns:
            Dictionary ready for json.dump()
        """
        return self.dict(exclude_none=True)
    
    def save_to_file(self, filepath: str) -> None:
        """
        Save manifest to JSON file
        
        Args:
            filepath: Path to save manifest.json
        """
        path = Path(filepath)
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(self.to_json_dict(), f, indent=2, ensure_ascii=False)
    
    @classmethod
    def load_from_file(cls, filepath: str) -> 'ManifestSchemaLite':
        """
        Load manifest from JSON file
        
        Args:
            filepath: Path to manifest.json
            
        Returns:
            ManifestSchemaLite instance
            
        Raises:
            ValueError: If manifest is invalid or not found
        """
        path = Path(filepath)
        if not path.exists():
            raise FileNotFoundError(f"Manifest file not found: {filepath}")
        
        try:
            with open(path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            # Parse tools
            tools = []
            for tool_data in data.get("tools", []):
                tool = ToolDeclarationLite.from_dict(tool_data)
                tools.append(tool)
            
            # Parse metadata
            metadata = None
            if "metadata" in data and data["metadata"]:
                metadata = ArtifactMetadataLite(**data["metadata"])
            
            # Create manifest
            return cls(
                version=data["version"],
                language=data["language"],
                entry_point=data["entry_point"],
                tools=tools,
                dependencies=data.get("dependencies", []),
                metadata=metadata,
                runtime_requirements=data.get("runtime_requirements", {}),
                security_policy=data.get("security_policy", {})
            )
        except Exception as e:
            raise ValueError(f"Failed to load manifest: {str(e)}") from e
    
    # Compatibility Checks
    def is_compatible_with_runtime(self, runtime_version: str) -> bool:
        """
        Check if this artifact is compatible with a given runtime version
        
        Args:
            runtime_version: Runtime version (SemVer)
            
        Returns:
            True if compatible (same major version)
        """
        def major_version(semver: str) -> int:
            return int(semver.split('.')[0])
        
        try:
            return major_version(self.version) == major_version(runtime_version)
        except (ValueError, IndexError):
            return False
    
    # Utility Methods
    def get_tool_names(self) -> List[str]:
        """Return list of all callable function names"""
        return [tool.name for tool in self.tools]
    
    def get_tool_by_name(self, name: str) -> Optional[ToolDeclarationLite]:
        """
        Find a tool by name
        
        Args:
            name: Function name
            
        Returns:
            ToolDeclarationLite or None if not found
        """
        for tool in self.tools:
            if tool.name == name:
                return tool
        return None
    
    def validate_integrity(self) -> bool:
        """
        Validate artifact integrity (placeholder for future checksum verification)
        
        Returns:
            True if integrity check passes
        """
        # TODO: Implement checksum verification
        # - Hash entry_point file
        # - Compare with stored checksum in metadata
        return True


# Export for convenience
__all__ = ['ManifestSchemaLite', 'ArtifactMetadataLite']


