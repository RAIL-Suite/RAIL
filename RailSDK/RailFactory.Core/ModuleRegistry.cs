namespace RailFactory.Core;

/// <summary>
/// Registry of all modules in a composite manifest.
/// Provides lazy initialization and function-to-module mapping.
/// 
/// DESIGN:
/// - Lazy instantiation: ModuleInstance created only on first access
/// - Function lookup: O(1) function→module resolution
/// - Thread-safe: Concurrent access supported
/// 
/// NAMING RESOLUTION:
/// When executing a function, the registry supports:
/// 1. Fully qualified: "ModuleId.FunctionName" → exact routing
/// 2. Unqualified: "FunctionName" → auto-discovery from function map
/// </summary>
public class ModuleRegistry : IDisposable
{
    private readonly CompositeManifest _manifest;
    private readonly string _basePath;
    private readonly Dictionary<string, ModuleInstance> _modules = new();
    private readonly Dictionary<string, ModuleManifest> _moduleManifests = new();
    private readonly Dictionary<string, string> _functionToModule = new();
    private readonly object _lock = new();
    private bool _isDisposed;
    
    /// <summary>
    /// Number of registered modules.
    /// </summary>
    public int ModuleCount => _manifest.Modules.Count;
    
    /// <summary>
    /// Number of active (instantiated) module instances.
    /// </summary>
    public int ActiveInstanceCount
    {
        get
        {
            lock (_lock)
            {
                return _modules.Count;
            }
        }
    }
    
    /// <summary>
    /// All module IDs in the registry.
    /// </summary>
    public IEnumerable<string> ModuleIds => _moduleManifests.Keys;
    
    /// <summary>
    /// Creates a new module registry from a composite manifest.
    /// </summary>
    /// <param name="manifest">Composite manifest containing module definitions</param>
    /// <param name="basePath">Base path where module files are located</param>
    public ModuleRegistry(CompositeManifest manifest, string basePath)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        
        // Build lookup tables
        foreach (var module in manifest.Modules)
        {
            _moduleManifests[module.ModuleId] = module;
            
            // Map each function to its module
            foreach (var tool in module.Tools)
            {
                var functionName = tool.Name;
                
                // Handle duplicate function names across modules
                if (_functionToModule.ContainsKey(functionName))
                {
                    // Duplicate detected - require qualified naming
                    // Remove from map so auto-discovery won't work
                    // (forces user to use ModuleId.FunctionName)
                    _functionToModule.Remove(functionName);
                }
                else
                {
                    _functionToModule[functionName] = module.ModuleId;
                }
            }
        }
    }
    
    /// <summary>
    /// Gets or creates a module instance by ID.
    /// Uses lazy initialization - instance created on first access.
    /// </summary>
    /// <param name="moduleId">Module identifier</param>
    /// <returns>Module instance ready for execution</returns>
    /// <exception cref="KeyNotFoundException">If module ID not found</exception>
    public ModuleInstance GetOrCreate(string moduleId)
    {
        ThrowIfDisposed();
        
        if (string.IsNullOrWhiteSpace(moduleId))
            throw new ArgumentException("Module ID cannot be null or empty", nameof(moduleId));
        
        lock (_lock)
        {
            if (_modules.TryGetValue(moduleId, out var existing))
                return existing;
            
            if (!_moduleManifests.TryGetValue(moduleId, out var manifest))
            {
                throw new KeyNotFoundException(
                    $"Module '{moduleId}' not found in manifest. " +
                    $"Available modules: {string.Join(", ", _moduleManifests.Keys)}");
            }
            
            var instance = new ModuleInstance(manifest, _basePath);
            _modules[moduleId] = instance;
            return instance;
        }
    }
    
    /// <summary>
    /// Finds the module ID that contains a given function.
    /// Returns null if function not found or if multiple modules have the same function name.
    /// </summary>
    /// <param name="functionName">Function name (without module prefix)</param>
    /// <returns>Module ID, or null if not uniquely resolvable</returns>
    public string? FindModuleForFunction(string functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            return null;
        
        return _functionToModule.TryGetValue(functionName, out var moduleId) 
            ? moduleId 
            : null;
    }
    
    /// <summary>
    /// Checks if a module exists in the registry.
    /// </summary>
    public bool HasModule(string moduleId)
    {
        return !string.IsNullOrWhiteSpace(moduleId) && _moduleManifests.ContainsKey(moduleId);
    }
    
    /// <summary>
    /// Gets the manifest for a specific module.
    /// </summary>
    public ModuleManifest? GetModuleManifest(string moduleId)
    {
        return _moduleManifests.TryGetValue(moduleId, out var manifest) ? manifest : null;
    }
    
    /// <summary>
    /// Throws if registry has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ModuleRegistry));
    }
    
    public void Dispose()
    {
        if (_isDisposed)
            return;
        
        lock (_lock)
        {
            foreach (var module in _modules.Values)
            {
                try
                {
                    module.Dispose();
                }
                catch
                {
                    // Suppress exceptions during cleanup
                }
            }
            
            _modules.Clear();
        }
        
        _isDisposed = true;
    }
}



