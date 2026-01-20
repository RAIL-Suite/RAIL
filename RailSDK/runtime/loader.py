"""
Rail Runtime - Artifact Loader
Fast loading and caching of Rail Artifacts

This module is optimized for:
- Sub-10ms manifest loading
- Memory-efficient caching
- Validation without full re-parsing
"""

import time
from pathlib import Path
from typing import Optional, List

# Add parent directory and Rail-bridge to path
import sys
parent_dir = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(parent_dir))
sys.path.insert(0, str(parent_dir / "Rail-bridge"))

from core.manifest import ManifestSchema
from core.schema import ToolDeclaration
from core.exceptions import ManifestValidationError, ArtifactIntegrityError


class LoadedArtifact:
    """
    Container for loaded artifact data
    
    This is returned by ArtifactLoader.load() and contains
    all parsed data in memory for fast access.
    """
    
    def __init__(self, manifest: ManifestSchema, load_time_ms: float):
        """
        Initialize loaded artifact
        
        Args:
            manifest: Parsed ManifestSchema
            load_time_ms: Time taken to load (milliseconds)
        """
        self.manifest = manifest
        self.load_time_ms = load_time_ms
        
        # Create lookup index for O(1) function access
        self._tool_index = {tool.name: tool for tool in manifest.tools}
    
    def get_tool(self, name: str) -> Optional[ToolDeclaration]:
        """Get tool by name (O(1) lookup)"""
        return self._tool_index.get(name)
    
    def has_tool(self, name: str) -> bool:
        """Check if tool exists (O(1) lookup)"""
        return name in self._tool_index
    
    def get_tool_names(self) -> List[str]:
        """Get all tool names"""
        return list(self._tool_index.keys())


class ArtifactLoader:
    """
    Artifact Loader - Fast manifest reading and validation
    
    Responsibilities:
    - Load manifest.json (optimized for speed)
    - Validate integrity
    - Cache parsed data in memory
    - Provide O(1) function lookup
    
    Performance Target: < 10ms load time for typical artifacts
    """
    
    def __init__(self, artifact_path: str):
        """
        Initialize artifact loader
        
        Args:
            artifact_path: Path to artifact directory (containing manifest.json)
        """
        self.artifact_path = Path(artifact_path)
        self.manifest_file = self.artifact_path / "manifest.json"
        
        # Cached data
        self._loaded_artifact: Optional[LoadedArtifact] = None
    
    def load(self) -> ManifestSchema:
        """
        Load and parse manifest.json
        
        Returns:
            Parsed ManifestSchema object
            
        Raises:
            ManifestValidationError: If manifest is invalid or missing
            ArtifactIntegrityError: If integrity check fails
        """
        start_time = time.perf_counter()
        
        # Validate artifact directory exists
        if not self.artifact_path.exists():
            raise ManifestValidationError(
                f"Artifact directory not found: {self.artifact_path}",
                {"path": str(self.artifact_path)}
            )
        
        # Validate manifest.json exists
        if not self.manifest_file.exists():
            raise ManifestValidationError(
                f"manifest.json not found in {self.artifact_path}",
                {"path": str(self.artifact_path)}
            )
        
        # Load manifest
        try:
            manifest = ManifestSchema.load_from_file(str(self.manifest_file))
        except Exception as e:
            raise ManifestValidationError(
                f"Failed to parse manifest.json: {str(e)}",
                {"path": str(self.manifest_file), "error": str(e)}
            )
        
        # Validate integrity
        if not manifest.validate_integrity():
            raise ArtifactIntegrityError(
                "Artifact integrity check failed",
                {"artifact_id": manifest.metadata.artifact_id}
            )
        
        # Validate entry point exists
        entry_point = self.artifact_path / manifest.entry_point
        if not entry_point.exists():
            raise ManifestValidationError(
                f"Entry point file not found: {manifest.entry_point}",
                {"entry_point": manifest.entry_point, "path": str(self.artifact_path)}
            )
        
        # Cache loaded artifact
        load_time_ms = (time.perf_counter() - start_time) * 1000
        self._loaded_artifact = LoadedArtifact(manifest, load_time_ms)
        
        return manifest
    
    def get_load_time_ms(self) -> float:
        """
        Get load time in milliseconds
        
        Returns:
            Load time (0.0 if not loaded yet)
        """
        if not self._loaded_artifact:
            return 0.0
        return self._loaded_artifact.load_time_ms
    
    def get_tool(self, name: str) -> Optional[ToolDeclaration]:
        """
        Get tool declaration by name (cached, O(1) lookup)
        
        Args:
            name: Function name
            
        Returns:
            ToolDeclaration or None if not found
        """
        if not self._loaded_artifact:
            return None
        return self._loaded_artifact.get_tool(name)
    
    def has_function(self, name: str) -> bool:
        """
        Check if function exists in artifact (O(1) lookup)
        
        Args:
            name: Function name
            
        Returns:
            True if function exists
        """
        if not self._loaded_artifact:
            return False
        return self._loaded_artifact.has_tool(name)
    
    def get_tool_names(self) -> List[str]:
        """
        Get all function names
        
        Returns:
            List of function names
        """
        if not self._loaded_artifact:
            return []
        return self._loaded_artifact.get_tool_names()
    
    def validate_compatibility(self, runtime_version: str = "1.0") -> bool:
        """
        Check if artifact is compatible with current runtime
        
        Args:
            runtime_version: Current runtime version (default: "1.0")
            
        Returns:
            True if compatible
        """
        if not self._loaded_artifact:
            return False
        
        return self._loaded_artifact.manifest.is_compatible_with_runtime(runtime_version)


