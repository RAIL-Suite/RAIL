"""
Rail Bridge - Core Interfaces (Lightweight - Zero Dependencies)

Abstract Base Classes for the Product Triad Architecture - Lightweight version
This module is identical to interfaces.py but uses schema_lite instead of schema.

This version is for Builder and Runtime CLI (zero external dependencies).
For Web/FastAPI with validation, use interfaces.py (with Pydantic).
"""

from abc import ABC, abstractmethod
from typing import List, Dict, Any, Optional

from .schema_lite import ToolDeclarationLite
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
    Abstract Parser Interface (Lightweight)
    
    Contract for all language parsers (Python, C#, JavaScript, etc.)
    Each parser is responsible for extracting function signatures and 
    converting them to the standard ToolDeclarationLite format for LLM consumption.
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
    def parse_source(self, source_code: str) -> List[ToolDeclarationLite]:
        """
        Extract function definitions from source code
        
        Args:
            source_code: Raw source code as string
            
        Returns:
            List of ToolDeclarationLite objects representing callable functions
            
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
            Dictionary with parse metrics
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
    Abstract Runner Interface (Lightweight)
    
    Contract for all execution environments (Local, Docker, Embedded, etc.)
    Each runner is responsible for loading code and executing functions
    with proper type handling and error management.
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
        """
        return [
            name for name in dir(module) 
            if callable(getattr(module, name)) and not name.startswith('_')
        ]
    
    def get_execution_metadata(self) -> Dict[str, Any]:
        """
        Return observability metrics from last execution
        
        Returns:
            Dictionary with execution metrics
        """
        return {
            "execution_duration_ms": 0.0,
            "dependencies_installed": []
        }
    
    def cleanup(self) -> None:
        """
        Cleanup resources (temp files, processes, containers, etc.)
        """
        pass


# =============================================================================
# CAPABILITY DISCOVERY
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


