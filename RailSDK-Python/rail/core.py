# ============================================================================
# Rail SDK CORE
# ============================================================================
# Main entry point for the Rail SDK. Provides the ignite() function
# which is the only thing users need to call.
#
# Usage:
#     import rail
#     Rail.ignite(my_app_instance)
#
# ============================================================================

import json
import uuid
import threading
from typing import Any, Optional

from .bridge import get_bridge
from .discovery import generate_manifest

# ============================================================================
# GLOBAL STATE
# ============================================================================

_instance: Optional[Any] = None
_lock = threading.Lock()

# ============================================================================
# CALLBACK EXECUTOR
# ============================================================================

def _execute_callback(command_json: str) -> str:
    """
    Execute a command received from the Host.
    
    Args:
        command_json: JSON containing method name and arguments
    
    Returns:
        JSON result with status and result/error
    """
    global _instance
    
    try:
        command = json.loads(command_json)
        method_name = command.get("method", "")
        args = command.get("args", {})
        
        if _instance is None:
            return json.dumps({
                "status": "error",
                "message": "No instance registered"
            })
        
        # Get the method
        method = getattr(_instance, method_name, None)
        if method is None:
            return json.dumps({
                "status": "error",
                "message": f"Method not found: {method_name}"
            })
        
        if not callable(method):
            return json.dumps({
                "status": "error", 
                "message": f"Not a callable: {method_name}"
            })
        
        # Execute the method
        if isinstance(args, dict):
            result = method(**args)
        elif isinstance(args, list):
            result = method(*args)
        else:
            result = method(args) if args else method()
        
        # Serialize result
        try:
            result_json = json.dumps(result)
            return json.dumps({
                "status": "success",
                "result": json.loads(result_json)  # Ensure it's serializable
            })
        except (TypeError, ValueError):
            # If result isn't JSON serializable, convert to string
            return json.dumps({
                "status": "success",
                "result": str(result)
            })
    
    except Exception as e:
        return json.dumps({
            "status": "error",
            "message": str(e)
        })

# ============================================================================
# PUBLIC API
# ============================================================================

def ignite(
    instance: Any,
    context: Optional[str] = None,
    include_private: bool = False
) -> bool:
    """
    Register an object with the Rail Host for AI-driven control.
    
    This is the main entry point for the Rail SDK. Call this once
    with your application instance, and all its public methods become
    available to LLMs.
    
    Args:
        instance: Object instance whose methods will be exposed
        context: Optional context name (defaults to class name)
        include_private: If True, also expose methods starting with _
    
    Returns:
        True if connection successful, False otherwise
    
    Example:
        class MyApp:
            def process_order(self, order_id: int) -> str:
                return f"Processed {order_id}"
        
        app = MyApp()
        Rail.ignite(app)  # That's it!
    """
    global _instance
    
    with _lock:
        if _instance is not None:
            raise RuntimeError("Already ignited. Call disconnect() first.")
        
        _instance = instance
    
    try:
        # Generate manifest
        manifest = generate_manifest(instance, context, include_private)
        manifest_json = json.dumps(manifest)
        
        # Generate unique instance ID
        instance_id = str(uuid.uuid4())
        
        # Connect to Host
        bridge = get_bridge()
        result = bridge.ignite(instance_id, manifest_json, _execute_callback)
        
        if result != 0:
            with _lock:
                _instance = None
            # Error codes from bridge
            errors = {
                -1: "Invalid argument",
                -2: "Null callback",
                -3: "Already initialized",
                -4: "Not initialized",
                -5: "Connection failed - Is Rail Host running?",
                -6: "Pipe broken",
                -7: "Connection timeout",
                -99: "Unknown error"
            }
            error_msg = errors.get(result, f"Error code: {result}")
            raise ConnectionError(f"Failed to ignite: {error_msg}")
        
        return True
    
    except Exception:
        with _lock:
            _instance = None
        raise

def disconnect():
    """
    Disconnect from the Rail Host and cleanup.
    
    Call this when shutting down your application or if you need
    to re-register with different settings.
    """
    global _instance
    
    with _lock:
        _instance = None
    
    bridge = get_bridge()
    bridge.disconnect()

def is_connected() -> bool:
    """
    Check if currently connected to the Rail Host.
    
    Returns:
        True if connected, False otherwise
    """
    bridge = get_bridge()
    return bridge.is_connected()


