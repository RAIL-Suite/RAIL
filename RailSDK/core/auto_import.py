"""
Conditional Import Wrapper for Core Modules

This module provides a unified import interface that automatically selects:
- Pydantic-based versions (schema.py, interfaces.py) when Pydantic is available (Web)
- Dataclass-based versions (schema_lite.py, interfaces_lite.py) when Pydantic is not available (CLI)

This allows the same codebase to work in both environments without modification.
"""

# Try to import Pydantic versions first (for Web/FastAPI)
try:
    from pydantic import BaseModel
    PYDANTIC_AVAILABLE = True
except ImportError:
    PYDANTIC_AVAILABLE = False


# Conditional imports based on Pydantic availability
if PYDANTIC_AVAILABLE:
    # Use Pydantic versions (for Web with validation)
    from .schema import ChatReq as ChatReq
    from .schema import ToolDeclaration as ToolDeclaration
    from .schema import FunctionResult as FunctionResult
    from .interfaces import BaseParser as BaseParser
    from .interfaces import BaseRunner as BaseRunner
    from .manifest import ManifestSchema as ManifestSchema
    from .manifest import ArtifactMetadata as ArtifactMetadata
else:
    # Use dataclass versions (for CLI without dependencies)
    from .schema_lite import ChatReqLite as ChatReq  # type: ignore
    from .schema_lite import ToolDeclarationLite as ToolDeclaration  # type: ignore
    from .schema_lite import FunctionResultLite as FunctionResult  # type: ignore
    from .interfaces_lite import BaseParser as BaseParser  # type: ignore
    from .interfaces_lite import BaseRunner as BaseRunner  # type: ignore
    from .manifest_lite import ManifestSchemaLite as ManifestSchema  # type: ignore
    from .manifest_lite import ArtifactMetadataLite as ArtifactMetadata  # type: ignore


__all__ = [
    'ChatReq',
    'ToolDeclaration',
    'FunctionResult',
    'BaseParser',
    'BaseRunner',
    'ManifestSchema',
    'ArtifactMetadata',
    'PYDANTIC_AVAILABLE'
]


