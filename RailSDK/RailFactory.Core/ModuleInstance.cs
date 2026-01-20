using RailFactory.Core.TransportClients;

namespace RailFactory.Core;

/// <summary>
/// Represents a running instance of a module with its transport connection.
/// Manages lazy initialization and lifecycle of the transport.
/// 
/// LIFECYCLE:
/// 1. Constructor: Creates instance, prepares transport (no connection yet)
/// 2. First Execute(): Connects transport (lazy init)
/// 3. Subsequent Execute(): Reuses transport
/// 4. Dispose(): Cleans up transport resources
/// 
/// THREAD SAFETY:
/// Execute() calls are thread-safe via locking.
/// Multiple concurrent calls will be serialized.
/// </summary>
public class ModuleInstance : IDisposable
{
    private readonly ModuleManifest _manifest;
    private readonly string _basePath;
    private readonly ITransportClient _transport;
    private readonly object _executeLock = new();
    private bool _isInitialized;
    private bool _isDisposed;
    
    /// <summary>
    /// Module identifier from manifest.
    /// </summary>
    public string ModuleId => _manifest.ModuleId;
    
    /// <summary>
    /// Full module manifest.
    /// </summary>
    public ModuleManifest Manifest => _manifest;
    
    /// <summary>
    /// Transport type being used (e.g., "namedpipe", "stdin").
    /// </summary>
    public string TransportType => _transport.TransportType;
    
    /// <summary>
    /// Indicates whether the module transport is connected.
    /// </summary>
    public bool IsConnected => _transport.IsConnected;
    
    /// <summary>
    /// Creates a new module instance.
    /// </summary>
    /// <param name="manifest">Module manifest containing configuration</param>
    /// <param name="basePath">Base path where module files are located</param>
    public ModuleInstance(ModuleManifest manifest, string basePath)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        
        // Create appropriate transport based on manifest
        _transport = TransportFactory.CreateForModule(manifest);
    }
    
    /// <summary>
    /// Executes a function on this module.
    /// Initializes transport on first call (lazy init).
    /// </summary>
    /// <param name="functionName">Name of the function to execute</param>
    /// <param name="argsJson">JSON-serialized arguments</param>
    /// <returns>JSON-serialized result</returns>
    public string Execute(string functionName, string argsJson)
    {
        ThrowIfDisposed();
        
        lock (_executeLock)
        {
            EnsureInitialized();
            return _transport.Execute(functionName, argsJson);
        }
    }
    
    /// <summary>
    /// Checks if the module is reachable.
    /// </summary>
    /// <returns>True if module responds to ping</returns>
    public bool Ping()
    {
        if (_isDisposed)
            return false;
        
        lock (_executeLock)
        {
            EnsureInitialized();
            return _transport.Ping();
        }
    }
    
    /// <summary>
    /// Initializes the transport if not already done.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;
        
        _transport.Initialize(_manifest, _basePath);
        _isInitialized = true;
    }
    
    /// <summary>
    /// Throws if this instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ModuleInstance), 
                $"Module '{ModuleId}' has been disposed.");
    }
    
    public void Dispose()
    {
        if (_isDisposed)
            return;
        
        _transport.Dispose();
        _isDisposed = true;
    }
}



