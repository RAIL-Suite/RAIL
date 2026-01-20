// ============================================================================
// Rail BRIDGE - NATIVE AOT C-ABI EXPORTS
// ============================================================================
// This module provides the universal connector between any language SDK
// and the Rail Host service. It exposes C-compatible functions that can
// be called from Python (ctypes), Node.js (ffi-napi), C++, Rust, Go, and .NET.
//
// ARCHITECTURE:
//   SDK (Python/Node/C++/.NET) → Bridge DLL (this) → Named Pipe → Rail Host
//
// ============================================================================

using System.Runtime.InteropServices;
using System.Text;

namespace RailBridge.Native;

/// <summary>
/// C-ABI compatible exports for the Rail Bridge.
/// </summary>
public static class Exports
{
    // ========================================================================
    // TYPE DEFINITIONS
    // ========================================================================
    
    /// <summary>
    /// Callback function signature for command execution.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr RailCommandCallback(IntPtr commandJson);
    
    // ========================================================================
    // STATE
    // ========================================================================
    
    private static BridgeState? _state;
    private static readonly object _lock = new();
    
    // ========================================================================
    // PUBLIC EXPORTS
    // ========================================================================
    
    /// <summary>
    /// Initialize the Rail Bridge and connect to the Host service.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "Rail_Ignite")]
    public static int Ignite(IntPtr instanceId, IntPtr jsonManifest, IntPtr onCommand)
    {
        return IgniteInternal(instanceId, jsonManifest, onCommand);
    }
    
    // Wrapper for P/Invoke from .NET
    // IgniteManaged removed due to missing DllExport support in NativeAOT build context
    
    private static int IgniteInternal(IntPtr instanceId, IntPtr jsonManifest, IntPtr onCommand)
    {
        try
        {
            var id = Marshal.PtrToStringUTF8(instanceId);
            var manifest = Marshal.PtrToStringUTF8(jsonManifest);
            
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(manifest))
                return ErrorCodes.InvalidArgument;
            
            if (onCommand == IntPtr.Zero)
                return ErrorCodes.NullCallback;
            
            lock (_lock)
            {
                if (_state != null)
                    return ErrorCodes.AlreadyInitialized;
                
                var callback = Marshal.GetDelegateForFunctionPointer<RailCommandCallback>(onCommand);
                _state = new BridgeState(id, manifest, callback);
            }
            
            return _state.Connect();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RailBridge] Ignite failed: {ex.Message}");
            return ErrorCodes.UnknownError;
        }
    }
    
    [UnmanagedCallersOnly(EntryPoint = "Rail_Disconnect")]
    public static void Disconnect()
    {
        DisconnectInternal();
    }
    
    public static void DisconnectInternal()
    {
        lock (_lock)
        {
            _state?.Dispose();
            _state = null;
        }
    }
    
    [UnmanagedCallersOnly(EntryPoint = "Rail_Heartbeat")]
    public static int Heartbeat()
    {
        return HeartbeatInternal();
    }
    
    public static int HeartbeatInternal()
    {
        lock (_lock)
        {
            if (_state == null)
                return ErrorCodes.NotInitialized;
            
            return _state.SendHeartbeat();
        }
    }
    
    [UnmanagedCallersOnly(EntryPoint = "Rail_GetVersion")]
    public static IntPtr GetVersion()
    {
        return VersionString.Pointer;
    }
    
    [UnmanagedCallersOnly(EntryPoint = "Rail_IsConnected")]
    public static int IsConnected()
    {
        return IsConnectedInternal();
    }
    
    public static int IsConnectedInternal()
    {
        lock (_lock)
        {
            return _state?.IsConnected == true ? 1 : 0;
        }
    }
}

// ============================================================================
// ERROR CODES
// ============================================================================

/// <summary>
/// Error codes returned by Bridge functions.
/// Negative values indicate errors.
/// </summary>
public static class ErrorCodes
{
    public const int Success = 0;
    public const int InvalidArgument = -1;
    public const int NullCallback = -2;
    public const int AlreadyInitialized = -3;
    public const int NotInitialized = -4;
    public const int ConnectionFailed = -5;
    public const int PipeBroken = -6;
    public const int Timeout = -7;
    public const int UnknownError = -99;
}

// ============================================================================
// VERSION STRING HELPER
// ============================================================================

/// <summary>
/// Static version string that lives for the lifetime of the process.
/// </summary>
internal static class VersionString
{
    private static readonly byte[] _version = Encoding.UTF8.GetBytes("2.0.0\0");
    private static GCHandle _handle;
    
    static VersionString()
    {
        _handle = GCHandle.Alloc(_version, GCHandleType.Pinned);
    }
    
    public static IntPtr Pointer => _handle.AddrOfPinnedObject();
}



