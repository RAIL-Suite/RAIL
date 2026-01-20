"""
Rail Runtime - SDK for Loading and Executing Rail Artifacts
Public API for integration into other applications

Usage:
    ```python
    from rail_runtime import railRuntime
    
    # Load artifact
    runtime = RailRuntime("./artifacts/my_app")
    runtime.load()
    
    # Execute function
    result = runtime.execute("calculate_tax", {"income": 50000})
    print(result)
    
    # List available functions
    functions = runtime.get_available_functions()
    ```
"""

import sys
from pathlib import Path
from typing import Any, Dict, List, Optional

# Add SDK root to path
sdk_root = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(sdk_root))

from runtime.loader import ArtifactLoader
from core.exceptions import (
    ArtifactException,
    ManifestValidationError,
    FunctionNotFoundError,
    RunnerException
)
from core.manifest import ManifestSchema
from runners import get_runner



class RailRuntime:
    """
    Rail Runtime - SDK for executing Rail Artifacts
    
    This is the main public API for integrating Rail artifacts
    into other applications.
    
    Features:
    - Fast artifact loading (manifest caching)
    - Type-safe function execution
    - Automatic dependency resolution
    - Metrics and observability
    
    Example:
        ```python
        runtime = RailRuntime("./artifacts/calculator")
        runtime.load()
        
        result = runtime.execute("add", {"a": 5, "b": 3})
        print(result)  # 8
        ```
    """
    
    def __init__(self, artifact_path: str, auto_install_deps: bool = True):
        """
        Initialize Rail Runtime
        
        Args:
            artifact_path: Path to artifact directory (containing manifest.json)
            auto_install_deps: Enable automatic dependency installation (default: True)
        """
        self.artifact_path = Path(artifact_path)
        self.auto_install_deps = auto_install_deps
        
        # Internal state
        self._loader: Optional[ArtifactLoader] = None
        self._manifest: Optional[ManifestSchema] = None
        self._runner = None
        self._module = None
        self._loaded = False
        
        print(f"🚀 RailRuntime initialized: {self.artifact_path.name}")
    
    def load(self) -> None:
        """
        Load artifact into memory
        
        This method:
        1. Loads and validates manifest.json
        2. Selects appropriate runner (Python/C#/JS)
        3. Loads source code into memory
        4. Resolves dependencies
        
        Raises:
            ArtifactException: If artifact is invalid or cannot be loaded
            ManifestValidationError: If manifest.json is malformed
        """
        if self._loaded:
            print("⚠️  Artifact already loaded")
            return
        
        print(f"📦 Loading artifact: {self.artifact_path}")
        
        # Step 1: Load manifest
        self._loader = ArtifactLoader(str(self.artifact_path))
        self._manifest = self._loader.load()
        
        print(f"   ✅ Manifest loaded: {len(self._manifest.tools)} functions")
        
        # Step 2: Get appropriate runner
        language = self._manifest.language
        
        # Validate runtime requirements
        if language == "csharp":
            self._validate_dotnet_runtime()
            
        self._runner = get_runner(f"dummy.{self._get_extension(language)}")
        
        # Configure auto-install
        if hasattr(self._runner, 'auto_install'):
            self._runner.auto_install = self.auto_install_deps
        
        print(f"   ✅ Runner selected: {language}")
    
    def _validate_dotnet_runtime(self):
        """Check if .NET Runtime is available for C# artifacts"""
        import subprocess
        try:
            result = subprocess.run(
                ["dotnet", "--version"],
                capture_output=True,
                text=True,
                timeout=5
            )
            if result.returncode != 0:
                raise RuntimeError("dotnet command failed")
        except (FileNotFoundError, subprocess.TimeoutExpired, RuntimeError):
            raise RunnerException(
                ".NET Runtime 8.0+ required for C# artifacts.",
                {
                    "solution": "Install .NET Runtime from https://dotnet.microsoft.com/download",
                    "language": "csharp"
                }
            )
        
        # Step 3: Load source code
        entry_point = self.artifact_path / self._manifest.entry_point
        if not entry_point.exists():
            raise ArtifactException(
                f"Entry point not found: {self._manifest.entry_point}",
                {"artifact_path": str(self.artifact_path)}
            )
        
        source_code = entry_point.read_text(encoding='utf-8')
        self._module = self._runner.load_module(source_code)
        
        print(f"   ✅ Module loaded: {self._manifest.entry_point}")
        
        self._loaded = True
        print(f"✅ Artifact ready for execution\n")
    
    def execute(
        self,
        function_name: str,
        args: Dict[str, Any],
        timeout_seconds: Optional[float] = None
    ) -> Any:
        """
        Execute a function from the loaded artifact
        
        Args:
            function_name: Name of the function to execute
            args: Dictionary of arguments (key=param_name, value=param_value)
            timeout_seconds: Optional execution timeout
            
        Returns:
            Function execution result
            
        Raises:
            FunctionNotFoundError: If function doesn't exist in artifact
            RunnerException: If execution fails
            
        Example:
            ```python
            result = runtime.execute("calculate_tax", {
                "income": 50000,
                "tax_rate": 0.25
            })
            ```
        """
        if not self._loaded:
            raise ArtifactException(
                "Artifact not loaded. Call load() first.",
                {"artifact_path": str(self.artifact_path)}
            )
        
        # Validate function exists
        if not self._loader.has_function(function_name):
            available = self.get_available_functions()
            raise FunctionNotFoundError(function_name, available)
        
        # Execute via runner
        try:
            result = self._runner.execute_function(
                self._module,
                function_name,
                args,
                timeout_seconds=timeout_seconds
            )
            return result
            
        except Exception as e:
            raise RunnerException(
                f"Execution failed for '{function_name}': {str(e)}",
                {"function": function_name, "args": args, "error": str(e)}
            )
    
    def get_available_functions(self) -> List[str]:
        """
        Get list of all callable functions in the artifact
        
        Returns:
            List of function names
        """
        if not self._loaded:
            return []
        
        return self._loader.get_tool_names()
    
    def get_function_info(self, function_name: str) -> Optional[Dict[str, Any]]:
        """
        Get detailed information about a function
        
        Args:
            function_name: Name of the function
            
        Returns:
            Dictionary with keys: name, description, parameters
            Returns None if function not found
        """
        if not self._loaded:
            return None
        
        tool = self._loader.get_tool(function_name)
        if not tool:
            return None
        
        return {
            "name": tool.name,
            "description": tool.description,
            "parameters": tool.parameters
        }
    
    def get_manifest(self) -> Optional[ManifestSchema]:
        """
        Get the loaded manifest
        
        Returns:
            ManifestSchema object or None if not loaded
        """
        return self._manifest
    
    def get_metadata(self) -> Dict[str, Any]:
        """
        Get artifact metadata
        
        Returns:
            Dictionary with artifact info (id, created_at, etc.)
        """
        if not self._manifest:
            return {}
        
        return {
            "artifact_id": self._manifest.metadata.artifact_id,
            "created_at": self._manifest.metadata.created_at,
            "language": self._manifest.language,
            "version": self._manifest.version,
            "entry_point": self._manifest.entry_point,
            "function_count": len(self._manifest.tools),
            "dependencies": self._manifest.dependencies
        }
    
    def get_execution_metrics(self) -> Dict[str, Any]:
        """
        Get performance metrics from last execution
        
        Returns:
            Dictionary with execution_duration_ms, dependencies_installed, etc.
        """
        if not self._runner:
            return {}
        
        return self._runner.get_execution_metadata()
    
    def cleanup(self) -> None:
        """
        Cleanup resources (temporary files, etc.)
        """
        if self._runner:
            self._runner.cleanup()
        
        self._loaded = False
        print(f"🧹 Runtime cleaned up")
    
    def _get_extension(self, language: str) -> str:
        """Map language to file extension"""
        mapping = {
            'python': 'py',
            'csharp': 'cs',
            'javascript': 'js',
            'typescript': 'ts'
        }
        return mapping.get(language.lower(), 'py')
    
    def __enter__(self):
        """Context manager support"""
        self.load()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager cleanup"""
        self.cleanup()
        return False


# Convenience function
def load_artifact(artifact_path: str, auto_install_deps: bool = True) -> RailRuntime:
    """
    Convenience function to load and return a runtime
    
    Args:
        artifact_path: Path to artifact directory
        auto_install_deps: Enable automatic dependency installation
        
    Returns:
        Loaded RailRuntime instance
    """
    runtime = RailRuntime(artifact_path, auto_install_deps)
    runtime.load()
    return runtime


