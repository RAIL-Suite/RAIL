# ============================================================================
# METHOD DISCOVERY
# ============================================================================
# Auto-discovery of methods using Python introspection.
# Generates manifest JSON for LLM consumption.
#
# ============================================================================

import inspect
import typing
from typing import List, Dict, Any, Optional, get_type_hints

from .types import FunctionInfo, ParameterInfo

# ============================================================================
# TYPE MAPPING
# ============================================================================

_TYPE_MAP = {
    int: "integer",
    float: "number",
    str: "string",
    bool: "boolean",
    list: "array",
    dict: "object",
    type(None): "null",
}

def _python_type_to_json(py_type: Any) -> str:
    """Convert Python type to JSON Schema type."""
    # Handle None/Optional first
    if py_type is type(None):
        return "null"
    
    # Handle Optional[X] -> X
    origin = getattr(py_type, "__origin__", None)
    if origin is typing.Union:
        args = py_type.__args__
        # Filter out NoneType for Optional handling
        non_none_args = [a for a in args if a is not type(None)]
        if len(non_none_args) == 1:
            return _python_type_to_json(non_none_args[0])
        return "any"
    
    # Handle List[X]
    if origin is list:
        return "array"
    
    # Handle Dict[K, V]
    if origin is dict:
        return "object"
    
    # Handle basic types
    return _TYPE_MAP.get(py_type, "any")

# ============================================================================
# DISCOVERY FUNCTIONS
# ============================================================================

def discover_methods(instance: object, include_private: bool = False) -> List[FunctionInfo]:
    """
    Discover all callable methods on an object instance.
    
    Args:
        instance: Object instance to inspect
        include_private: If True, include methods starting with _
    
    Returns:
        List of function descriptors suitable for manifest
    """
    functions: List[FunctionInfo] = []
    
    # Get the class and its methods
    cls = instance.__class__
    
    for name in dir(instance):
        # Skip magic methods
        if name.startswith("__"):
            continue
        
        # Skip private unless requested
        if name.startswith("_") and not include_private:
            continue
        
        attr = getattr(instance, name)
        
        # Only include callable methods
        if not callable(attr):
            continue
        
        # Skip bound methods from base object class
        if hasattr(object, name):
            continue
        
        # Extract function info
        func_info = _extract_function_info(attr, name)
        if func_info:
            functions.append(func_info)
    
    return functions

def _extract_function_info(func: callable, name: str) -> Optional[FunctionInfo]:
    """Extract function information from a callable."""
    try:
        sig = inspect.signature(func)
        doc = inspect.getdoc(func) or ""
        
        # Try to get type hints
        try:
            hints = get_type_hints(func)
        except Exception:
            hints = {}
        
        parameters: List[ParameterInfo] = []
        
        for param_name, param in sig.parameters.items():
            # Skip 'self' for methods
            if param_name == "self":
                continue
            
            param_type = hints.get(param_name, inspect.Parameter.empty)
            
            param_info: ParameterInfo = {
                "name": param_name,
                "type": _python_type_to_json(param_type) if param_type != inspect.Parameter.empty else "any",
                "description": "",  # Could extract from docstring parsing
                "required": param.default == inspect.Parameter.empty
            }
            
            parameters.append(param_info)
        
        return {
            "name": name,
            "description": doc.split("\n")[0] if doc else "",  # First line of docstring
            "parameters": parameters
        }
    
    except Exception:
        # If we can't inspect, skip this method
        return None

# ============================================================================
# MANIFEST GENERATION
# ============================================================================

def generate_manifest(
    instance: object,
    context: Optional[str] = None,
    include_private: bool = False
) -> Dict[str, Any]:
    """
    Generate a complete manifest for an object instance.
    
    Args:
        instance: Object to generate manifest for
        context: Optional context name
        include_private: If True, include private methods
    
    Returns:
        Complete manifest dictionary
    """
    import os
    from .version import __version__
    
    functions = discover_methods(instance, include_private)
    
    return {
        "processId": os.getpid(),
        "language": "python",
        "sdkVersion": __version__,
        "context": context or instance.__class__.__name__,
        "functions": functions
    }


