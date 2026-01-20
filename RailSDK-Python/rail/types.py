# ============================================================================
# TYPE DEFINITIONS FOR Rail SDK
# ============================================================================
# Type hints and protocol definitions for type-safe usage.
#
# ============================================================================

from typing import TypedDict, List, Optional, Callable, Any, Protocol
from dataclasses import dataclass

# ============================================================================
# MANIFEST TYPES
# ============================================================================

class ParameterInfo(TypedDict):
    """Parameter descriptor for a function."""
    name: str
    type: str
    description: str
    required: bool

class FunctionInfo(TypedDict):
    """Function descriptor for manifest."""
    name: str
    description: str
    parameters: List[ParameterInfo]

class Manifest(TypedDict):
    """Complete manifest sent to Host."""
    processId: int
    language: str
    sdkVersion: str
    context: Optional[str]
    functions: List[FunctionInfo]

# ============================================================================
# CALLBACK PROTOCOL
# ============================================================================

@dataclass
class CommandPayload:
    """Command received from Host."""
    method: str
    args: dict

@dataclass
class ResultPayload:
    """Result to send back to Host."""
    status: str  # "success" or "error"
    result: Optional[Any] = None
    message: Optional[str] = None

class ExecuteCallback(Protocol):
    """Protocol for command execution callback."""
    def __call__(self, command_json: str) -> str:
        """Execute a command and return result JSON."""
        ...


