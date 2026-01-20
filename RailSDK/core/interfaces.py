"""
Rail Bridge - Core Interfaces (Enterprise Edition)
Abstract Base Classes for the Product Triad Architecture

This module defines the contracts that all parsers and runners must respect.
These interfaces ensure compatibility across:
- Rail Web (FastAPI server)
- Rail Builder (CLI packaging tool)
- Rail Runtime (SDK library)

Enterprise Features:
- Custom exception hierarchy for precise error handling
- Observability hooks for metrics and telemetry
- Versioning and capability discovery
"""

from abc import ABC, abstractmethod
from typing import List, Dict, Any, Optional
from datetime import datetime

from .schema import ToolDeclaration
from .exceptions import (
    ParserException,
    RunnerException,
    SyntaxValidationError,
    ModuleLoadError,
    FunctionNotFoundError
)


# =============================================================================
# PARSER INTERFACE
# =============================================================================

class BaseParser(ABC):
    """
    Abstract Parser Interface
    
    Contract for all language parsers (Python, C#, JavaScript, etc.)
    Each parser is responsible for extracting function signatures and 
    converting them to the standard ToolDeclaration format for LLM consumption.
    
    Enterprise Features:
    - Validates syntax before parsing
    - Collects parse-time metrics
    - Reports language version compatibility
    """
    
    @property
    @abstractmethod
    def supported_language(self) -> str:
        """
        Return language identifier (e.g., 'python', 'csharp', 'javascript')
        Used for artifact validation and routing
        """
        pass
    
    @property
    def parser_version(self) -> str:
        """
        Return parser version (SemVer format)
        Override this in subclasses for version tracking
        """
        return "1.0.0"
    
    @abstractmethod
    def parse_source(self, source_code: str) -> List[ToolDeclaration]:
        """
        Extract function definitions from source code
        
        Args:
            source_code: Raw source code as string
            
        Returns:
            List of ToolDeclaration objects representing callable functions
            
        Raises:
            SyntaxValidationError: If source code has syntax errors
            ParserException: For other parsing failures
        """
        pass
    
    def validate_syntax(self, source_code: str) -> bool:
        """
        Validate source code syntax without full parsing (fast pre-check)
        
        Args:
            source_code: Raw source code as string
            
        Returns:
            True if syntax is valid, False otherwise
            
        Note:
            Default implementation delegates to parse_source (override for performance)
        """
        try:
            self.parse_source(source_code)
            return True
        except SyntaxValidationError:
            return False
    
    def get_parse_metadata(self) -> Dict[str, Any]:
        """
        Return observability metrics from last parse operation
        
        Returns:
            Dictionary with keys:
            - parse_duration_ms: Time taken to parse (float)
            - function_count: Number of functions extracted (int)
            - ast_depth: Maximum AST nesting level (int, optional)
            - warnings: List of non-fatal issues (List[str], optional)
            
        Note:
            Override this method to provide detailed metrics for enterprise monitoring
        """
        return {
            "parse_duration_ms": 0.0,
            "function_count": 0,
            "warnings": []
        }


# =============================================================================
# RUNNER INTERFACE
# =============================================================================

class BaseRunner(ABC):
    """
    Abstract Runner Interface
    
    Contract for all execution environments (Local, Docker, Embedded, etc.)
    Each runner is responsible for loading code and executing functions
    with proper type handling and error management.
    
    Enterprise Features:
    - Dependency resolution and auto-installation
    - Execution timeout enforcement
    - Security sandboxing (optional)
    - Performance metrics collection
    """
    
    @property
    @abstractmethod
    def supported_language(self) -> str:
        """
        Return language identifier (must match parser)
        Used for validation in multi-language environments
        """
        pass
    
    @property
    def runner_version(self) -> str:
        """
        Return runner version (SemVer format)
        Override this in subclasses for version tracking
        """
        return "1.0.0"
    
    @property
    def execution_mode(self) -> str:
        """
        Return execution mode (e.g., 'native', 'docker', 'embedded')
        Used for telemetry and debugging
        """
        return "native"
    
    @abstractmethod
    def load_module(self, source_code: str, module_name: Optional[str] = None) -> Any:
        """
        Load source code into memory and prepare for execution
        
        Args:
            source_code: Raw source code as string
            module_name: Optional name for the module (for caching/debugging)
            
        Returns:
            Module object or execution context (runner-specific)
            
        Raises:
            ModuleLoadError: If code compilation/loading fails
            DependencyResolutionError: If required imports cannot be resolved
        """
        pass
    
    @abstractmethod
    def execute_function(
        self, 
        module: Any, 
        func_name: str, 
        args: Dict[str, Any],
        timeout_seconds: Optional[float] = None
    ) -> Any:
        """
        Execute a specific function from the loaded module
        
        Args:
            module: The module object returned by load_module()
            func_name: Name of the function to execute
            args: Dictionary of argument name -> value
            timeout_seconds: Maximum execution time (None = no limit)
            
        Returns:
            Function execution result (any JSON-serializable type)
            
        Raises:
            FunctionNotFoundError: If function not found in module
            TypeMismatchError: If argument types are incompatible
            ExecutionTimeoutError: If execution exceeds timeout
            RunnerException: For other execution failures
        """
        pass
    
    def get_available_functions(self, module: Any) -> List[str]:
        """
        List all callable functions in the loaded module
        
        Args:
            module: Module object from load_module()
            
        Returns:
            List of function names (strings)
            
        Note:
            Default implementation uses dir() + callable() check
            Override for language-specific introspection
        """
        return [
            name for name in dir(module) 
            if callable(getattr(module, name)) and not name.startswith('_')
        ]
    
    def get_execution_metadata(self) -> Dict[str, Any]:
        """
        Return observability metrics from last execution
        
        Returns:
            Dictionary with keys:
            - execution_duration_ms: Time taken to execute (float)
            - memory_usage_mb: Peak memory consumption (float, optional)
            - cpu_time_ms: CPU time consumed (float, optional)
            - dependencies_installed: List of packages installed (List[str])
            
        Note:
            Override this method to provide detailed metrics for enterprise monitoring
        """
        return {
            "execution_duration_ms": 0.0,
            "dependencies_installed": []
        }
    
    def cleanup(self) -> None:
        """
        Cleanup resources (temp files, processes, containers, etc.)
        
        Note:
            Override this for runners that allocate persistent resources
            Called automatically in context managers (if implemented)
        """
        pass


# =============================================================================
# CAPABILITY DISCOVERY (for Runtime version compatibility)
# =============================================================================

def get_interface_version() -> str:
    """
    Return the version of the interface contracts
    Used by Runtime to validate parser/runner compatibility
    """
    return "1.0.0"


def is_compatible(parser_version: str, runner_version: str, interface_version: str) -> bool:
    """
    Check if parser/runner versions are compatible with current interface
    
    Args:
        parser_version: Version string from BaseParser.parser_version
        runner_version: Version string from BaseRunner.runner_version
        interface_version: Result from get_interface_version()
        
    Returns:
        True if versions are compatible (same major version)
    """
    def major_version(semver: str) -> int:
        return int(semver.split('.')[0])
    
    try:
        interface_major = major_version(interface_version)
        return (
            major_version(parser_version) == interface_major and
            major_version(runner_version) == interface_major
        )
    except (ValueError, IndexError):
        return False  # Invalid version format = incompatible



