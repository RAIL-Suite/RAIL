# ============================================================================
# NATIVE BRIDGE INTERFACE
# ============================================================================
# ctypes wrapper for RailBridge.dll native library.
# Handles loading the native library and managing callbacks.
#
# ============================================================================

import ctypes
import os
import sys
import platform
from pathlib import Path
from typing import Optional, Callable

# ============================================================================
# LIBRARY LOADING
# ============================================================================

def _get_lib_path() -> Path:
    """Get the path to the native bridge library."""
    lib_dir = Path(__file__).parent
    
    system = platform.system()
    if system == "Windows":
        lib_name = "RailBridge.dll"
    elif system == "Linux":
        lib_name = "libRailBridge.so"
    elif system == "Darwin":
        lib_name = "libRailBridge.dylib"
    else:
        raise RuntimeError(f"Unsupported platform: {system}")
    
    lib_path = lib_dir / lib_name
    
    if not lib_path.exists():
        raise FileNotFoundError(
            f"Native bridge library not found: {lib_path}\n"
            f"Make sure RailBridge is installed for your platform."
        )
    
    return lib_path

def _load_library() -> ctypes.CDLL:
    """Load the native bridge library."""
    lib_path = _get_lib_path()
    
    if platform.system() == "Windows":
        return ctypes.WinDLL(str(lib_path))
    else:
        return ctypes.CDLL(str(lib_path))

# ============================================================================
# CALLBACK TYPE
# ============================================================================

# typedef const char* (*RailCommandCallback)(const char* commandJson);
RailCommandCallback = ctypes.CFUNCTYPE(ctypes.c_char_p, ctypes.c_char_p)

# ============================================================================
# BRIDGE CLASS
# ============================================================================

class NativeBridge:
    """
    Wrapper for the native RailBridge library.
    Manages loading, function binding, and callback lifecycle.
    """
    
    _instance: Optional["NativeBridge"] = None
    _lib: Optional[ctypes.CDLL] = None
    _callback: Optional[RailCommandCallback] = None
    _callback_fn: Optional[Callable[[str], str]] = None
    
    def __new__(cls):
        """Singleton pattern for bridge instance."""
        if cls._instance is None:
            cls._instance = super().__new__(cls)
            cls._instance._init_bridge()
        return cls._instance
    
    def _init_bridge(self):
        """Initialize the native bridge."""
        self._lib = _load_library()
        
        # int Rail_Ignite(const char* instanceId, const char* jsonManifest, RailCommandCallback onCommand)
        self._lib.Rail_Ignite.argtypes = [ctypes.c_char_p, ctypes.c_char_p, RailCommandCallback]
        self._lib.Rail_Ignite.restype = ctypes.c_int
        
        # void Rail_Disconnect()
        self._lib.Rail_Disconnect.argtypes = []
        self._lib.Rail_Disconnect.restype = None
        
        # int Rail_Heartbeat()
        self._lib.Rail_Heartbeat.argtypes = []
        self._lib.Rail_Heartbeat.restype = ctypes.c_int
        
        # const char* Rail_GetVersion()
        self._lib.Rail_GetVersion.argtypes = []
        self._lib.Rail_GetVersion.restype = ctypes.c_char_p
        
        # int Rail_IsConnected()
        self._lib.Rail_IsConnected.argtypes = []
        self._lib.Rail_IsConnected.restype = ctypes.c_int
    
    def ignite(self, instance_id: str, manifest: str, callback: Callable[[str], str]) -> int:
        """
        Initialize connection to Host and start listening for commands.
        
        Args:
            instance_id: UUID identifying this application instance
            manifest: JSON manifest of available functions
            callback: Function to handle incoming commands
        
        Returns:
            0 on success, negative error code on failure
        """
        # Store callback to prevent garbage collection
        self._callback_fn = callback
        
        def _wrapped_callback(command_json: bytes) -> bytes:
            try:
                result = self._callback_fn(command_json.decode('utf-8'))
                return result.encode('utf-8')
            except Exception as e:
                import json
                error = json.dumps({"status": "error", "message": str(e)})
                return error.encode('utf-8')
        
        self._callback = RailCommandCallback(_wrapped_callback)
        
        result = self._lib.Rail_Ignite(
            instance_id.encode('utf-8'),
            manifest.encode('utf-8'),
            self._callback
        )
        
        return result
    
    def disconnect(self):
        """Disconnect from Host and cleanup."""
        if self._lib:
            self._lib.Rail_Disconnect()
        self._callback = None
        self._callback_fn = None
    
    def heartbeat(self) -> int:
        """Send heartbeat to Host."""
        if self._lib:
            return self._lib.Rail_Heartbeat()
        return -1
    
    def get_version(self) -> str:
        """Get native bridge version."""
        if self._lib:
            result = self._lib.Rail_GetVersion()
            if result:
                return result.decode('utf-8')
        return "unknown"
    
    def is_connected(self) -> bool:
        """Check if connected to Host."""
        if self._lib:
            return self._lib.Rail_IsConnected() == 1
        return False

# ============================================================================
# GLOBAL BRIDGE INSTANCE
# ============================================================================

def get_bridge() -> NativeBridge:
    """Get the singleton bridge instance."""
    return NativeBridge()


