"""
Rail Bridge - Manifest Schema
Standardized format for Rail Artifacts (Builder output / Runtime input)

The Manifest is the "contract" between Builder and Runtime:
- Builder generates manifest.json from source code
- Runtime loads manifest.json for instant parsing (no re-analysis)
- Ensures cross-product compatibility (Web, Builder, Runtime)
"""

from typing import List, Dict, Any, Optional
from datetime import datetime
from pydantic import BaseModel, Field, validator

from .schema import ToolDeclaration


class ArtifactMetadata(BaseModel):
    """
    Metadata about the artifact (authorship, build info, etc.)
    """
    artifact_id: str = Field(
        ...,
        description="Unique identifier for this artifact (UUID or hash)"
    )
    
    created_at: str = Field(
        default_factory=lambda: datetime.utcnow().isoformat(),
        description="ISO 8601 timestamp of artifact creation"
    )
    
    author: Optional[str] = Field(
        None,
        description="Author name or email"
    )
    
    description: Optional[str] = Field(
        None,
        description="Human-readable description of the artifact"
    )
    
    source_file: Optional[str] = Field(
        None,
        description="Original source filename (e.g., 'my_script.py')"
    )
    
    build_info: Dict[str, Any] = Field(
        default_factory=dict,
        description="Build environment info (builder version, platform, etc.)"
    )
    
    tags: List[str] = Field(
        default_factory=list,
        description="Tags for categorization (e.g., ['finance', 'analytics'])"
    )


class ManifestSchema(BaseModel):
    """
    Standardized Rail Artifact Manifest (v1.0)
    
    This is the core contract between Builder and Runtime.
    All fields are designed for forward/backward compatibility.
    
    Example:
        ```python
        manifest = ManifestSchema(
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
    version: str = Field(
        ...,
        description="Manifest schema version (SemVer, e.g., '1.0')"
    )
    
    language: str = Field(
        ...,
        description="Programming language (e.g., 'python', 'csharp', 'javascript')"
    )
    
    entry_point: str = Field(
        ...,
        description="Main source file to execute (e.g., 'source_logic.py')"
    )
    
    tools: List[ToolDeclaration] = Field(
        ...,
        description="List of callable functions/tools in the artifact"
    )
    
    # Optional Fields
    dependencies: List[str] = Field(
        default_factory=list,
        description="External dependencies with pinned versions (e.g., ['pandas==2.0.0'])"
    )
    
    metadata: ArtifactMetadata = Field(
        default_factory=ArtifactMetadata,
        description="Artifact metadata (author, build info, etc.)"
    )
    
    runtime_requirements: Dict[str, Any] = Field(
        default_factory=dict,
        description="Runtime requirements (min_python_version, memory_mb, etc.)"
    )
    
    security_policy: Dict[str, Any] = Field(
        default_factory=dict,
        description="Security constraints (allowed_imports, network_access, etc.)"
    )
    
    # Validators
    @validator('version')
    def validate_version(cls, v):
        """Ensure version follows SemVer format"""
        parts = v.split('.')
        if len(parts) < 2 or not all(p.isdigit() for p in parts[:2]):
            raise ValueError(f"Invalid version format: {v}. Expected SemVer (e.g., '1.0' or '1.0.0')")
        return v
    
    @validator('language')
    def validate_language(cls, v):
        """Ensure language is supported"""
        supported = ['python', 'csharp', 'javascript', 'typescript']
        if v.lower() not in supported:
            raise ValueError(
                f"Unsupported language: {v}. "
                f"Supported: {', '.join(supported)}"
            )
        return v.lower()
    
    @validator('tools')
    def validate_tools(cls, v):
        """Ensure at least one tool is defined"""
        if not v:
            raise ValueError("Manifest must contain at least one tool")
        return v
    
    # Serialization Helpers
    def to_json_dict(self) -> Dict[str, Any]:
        """
        Convert to JSON-serializable dictionary
        
        Returns:
            Dictionary ready for json.dump()
        """
        return self.dict(exclude_none=True, by_alias=True)
    
    def save_to_file(self, filepath: str) -> None:
        """
        Save manifest to JSON file
        
        Args:
            filepath: Path to save manifest.json
        """
        import json
        from pathlib import Path
        
        path = Path(filepath)
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(self.to_json_dict(), f, indent=2, ensure_ascii=False)
    
    @classmethod
    def load_from_file(cls, filepath: str) -> 'ManifestSchema':
        """
        Load manifest from JSON file
        
        Args:
            filepath: Path to manifest.json
            
        Returns:
            ManifestSchema instance
            
        Raises:
            ManifestValidationError: If manifest is invalid
        """
        import json
        from pathlib import Path
        from .exceptions import ManifestValidationError
        
        path = Path(filepath)
        if not path.exists():
            raise ManifestValidationError(
                f"Manifest file not found: {filepath}",
                {"filepath": filepath}
            )
        
        try:
            with open(path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            return cls(**data)
        except Exception as e:
            raise ManifestValidationError(
                f"Failed to load manifest: {str(e)}",
                {"filepath": filepath, "error": str(e)}
            )
    
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
    
    def get_tool_by_name(self, name: str) -> Optional[ToolDeclaration]:
        """
        Find a tool by name
        
        Args:
            name: Function name
            
        Returns:
            ToolDeclaration or None if not found
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
__all__ = ['ManifestSchema', 'ArtifactMetadata']


