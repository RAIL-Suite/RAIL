"""
Rail Bridge - Exception Hierarchy
Custom exceptions for granular error handling across the Product Triad

These exceptions enable:
- Precise error catching in enterprise integrations
- Structured logging with context
- User-friendly error messages in CLI/UI
"""


# =============================================================================
# BASE EXCEPTIONS
# =============================================================================

class RailBridgeException(Exception):
    """Base exception for all Rail Bridge errors"""
    
    def __init__(self, message: str, context: dict = None):
        super().__init__(message)
        self.message = message
        self.context = context or {}
    
    def to_dict(self) -> dict:
        """Convert exception to structured log format"""
        return {
            "error_type": self.__class__.__name__,
            "message": self.message,
            "context": self.context
        }


# =============================================================================
# PARSER EXCEPTIONS
# =============================================================================

class ParserException(RailBridgeException):
    """Base exception for all parser errors"""
    pass


class SyntaxValidationError(ParserException):
    """Raised when source code has syntax errors"""
    pass


class UnsupportedLanguageFeatureError(ParserException):
    """Raised when parser encounters unsupported syntax (e.g., async, decorators)"""
    pass


class TypeExtractionError(ParserException):
    """Raised when type hints cannot be parsed or are invalid"""
    pass


class DocstringParseError(ParserException):
    """Raised when docstring format is invalid (e.g., malformed Google/Numpy style)"""
    pass


# =============================================================================
# RUNNER EXCEPTIONS
# =============================================================================

class RunnerException(RailBridgeException):
    """Base exception for all runner errors"""
    pass


class ModuleLoadError(RunnerException):
    """Raised when source code cannot be loaded/compiled"""
    pass


class DependencyResolutionError(RunnerException):
    """Raised when required dependencies cannot be installed or imported"""
    pass


class FunctionNotFoundError(RunnerException):
    """Raised when requested function doesn't exist in the module"""
    
    def __init__(self, function_name: str, available_functions: list = None):
        super().__init__(
            f"Function '{function_name}' not found",
            {"function_name": function_name, "available": available_functions or []}
        )
        self.function_name = function_name
        self.available_functions = available_functions or []


class TypeMismatchError(RunnerException):
    """Raised when argument types don't match function signature"""
    
    def __init__(self, param_name: str, expected_type: str, actual_type: str):
        super().__init__(
            f"Type mismatch for '{param_name}': expected {expected_type}, got {actual_type}",
            {"param": param_name, "expected": expected_type, "actual": actual_type}
        )


class ExecutionTimeoutError(RunnerException):
    """Raised when function execution exceeds timeout limit"""
    
    def __init__(self, function_name: str, timeout_seconds: float):
        super().__init__(
            f"Function '{function_name}' exceeded timeout of {timeout_seconds}s",
            {"function": function_name, "timeout": timeout_seconds}
        )


class SecurityViolationError(RunnerException):
    """Raised when code attempts forbidden operations (file I/O, network, etc.)"""
    pass


# =============================================================================
# ARTIFACT EXCEPTIONS (for Builder/Runtime)
# =============================================================================

class ArtifactException(RailBridgeException):
    """Base exception for artifact-related errors"""
    pass


class ManifestValidationError(ArtifactException):
    """Raised when manifest.json is invalid or missing required fields"""
    pass


class ArtifactIntegrityError(ArtifactException):
    """Raised when artifact checksum/signature verification fails"""
    pass


class UnsupportedArtifactVersionError(ArtifactException):
    """Raised when artifact version is incompatible with current runtime"""
    
    def __init__(self, artifact_version: str, runtime_version: str):
        super().__init__(
            f"Artifact version {artifact_version} incompatible with runtime {runtime_version}",
            {"artifact_version": artifact_version, "runtime_version": runtime_version}
        )


