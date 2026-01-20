"""
Core Schema Definitions (Lightweight - Zero Dependencies)

This is the dataclass version of schema.py for use in Builder and Runtime CLI.
Uses only Python stdlib (no Pydantic) for maximum portability and zero-install distribution.

For Web/FastAPI usage, see schema.py (with Pydantic validation).
"""
from dataclasses import dataclass, field, asdict
from typing import List, Dict, Any, Optional


@dataclass
class ChatReqLite:
    """Chat request from frontend (lightweight)"""
    message: str
    
    def dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class ToolDeclarationLite:
    """
    Tool declaration for LLM (lightweight)
    
    Compatible with Gemini API function calling format.
    """
    name: str
    description: str
    parameters: Dict[str, Any]
    
    def dict(self) -> Dict[str, Any]:
        """Convert to dictionary (Pydantic-compatible API)"""
        return asdict(self)
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'ToolDeclarationLite':
        """Create from dictionary"""
        return cls(
            name=data["name"],
            description=data["description"],
            parameters=data["parameters"]
        )


@dataclass
class FunctionResultLite:
    """Result from function execution (lightweight)"""
    success: bool
    result: Any
    error: Optional[str] = None
    
    def dict(self) -> Dict[str, Any]:
        return asdict(self)


# Export for convenience
__all__ = ['ChatReqLite', 'ToolDeclarationLite', 'FunctionResultLite']


