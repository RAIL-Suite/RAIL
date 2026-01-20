namespace RailFactory.Core;

/// <summary>
/// Abstraction for communication between RailEngine and module processes.
/// Enables polyglot support - same interface for Named Pipes, stdin/stdout, HTTP, etc.
/// 
/// ENTERPRISE DESIGN NOTES:
/// - Implementations are stateful (hold connection state)
/// - Lazy connection: Connect() called only when first Execute() is needed
/// - Thread-safe: implementations must handle concurrent Execute() calls
/// - Disposable: cleanup resources on engine shutdown
/// </summary>
public interface ITransportClient : IDisposable
{
    /// <summary>
    /// Unique identifier for this transport type.
    /// Used for logging and debugging.
    /// Examples: "namedpipe", "stdin", "http"
    /// </summary>
    string TransportType { get; }
    
    /// <summary>
    /// Initializes connection parameters without connecting.
    /// Actual connection happens on first Execute() call (lazy init).
    /// </summary>
    /// <param name="module">Module manifest containing entry point and configuration</param>
    /// <param name="basePath">Base path where the module files are located</param>
    void Initialize(ModuleManifest module, string basePath);
    
    /// <summary>
    /// Executes a function on the target module.
    /// Establishes connection on first call if not already connected.
    /// </summary>
    /// <param name="functionName">Name of the function to execute</param>
    /// <param name="argsJson">JSON-serialized arguments</param>
    /// <returns>JSON-serialized result from the function</returns>
    /// <exception cref="InvalidOperationException">If connection fails or function not found</exception>
    string Execute(string functionName, string argsJson);
    
    /// <summary>
    /// Indicates whether the transport is currently connected and ready.
    /// For transports without persistent connections (e.g., stdin), always returns true after Initialize().
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Attempts to verify the target module is reachable.
    /// For Named Pipes: checks if pipe exists
    /// For stdin: checks if entry point file exists
    /// For HTTP: sends health check request
    /// </summary>
    /// <returns>True if module appears reachable</returns>
    bool Ping();
}



