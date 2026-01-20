namespace RailFactory.Core;

/// <summary>
/// Central registry for runtime plugins. Manages detection, scanning, and execution
/// of different binary types (.NET, Java, Python, etc.)
/// 
/// This is the core of the plugin architecture - adding a new runtime requires:
/// 1. Implement IRuntimeDetector, IRuntimeScanner, IRuntimeExecutor
/// 2. Call RegisterRuntime() in static constructor
/// 3. Toggle enabled flag in runtime_plugins.json
/// </summary>
public class RuntimeRegistry
{
    private static readonly List<IRuntimeDetector> _detectors = new();
    private static readonly Dictionary<RuntimeType, IRuntimeScanner> _scanners = new();
    private static readonly Dictionary<RuntimeType, Func<IRuntimeExecutor>> _executorFactories = new();
    
    static RuntimeRegistry()
    {
        // ═══════════════════════════════════════════════════════════════
        // .NET PLUGIN REGISTRATION (v1.0 - Implemented)
        // ═══════════════════════════════════════════════════════════════
        RegisterRuntime(
            RuntimeType.DotNetBinary,
            new DotNetRuntimeDetector(),
            new DotNetRuntimeScanner(),
            () => new DotNetRuntimeExecutor()
        );
        
        // ═══════════════════════════════════════════════════════════════
        // FUTURE PLUGIN REGISTRATIONS (Stubs - v1.1+)
        // ═══════════════════════════════════════════════════════════════
        // When implementing Java support, uncomment this:
        // RegisterRuntime(
        //     RuntimeType.JavaBinary,
        //     new JavaRuntimeDetector(),
        //     new JavaRuntimeScanner(),
        //     () => new JavaRuntimeExecutor()
       // );
        
        // When implementing Python support, uncomment this:
        // RegisterRuntime(
        //     RuntimeType.PythonBinary,
        //     new PythonRuntimeDetector(),
        //     new PythonRuntimeScanner(),
        //     () => new PythonRuntimeExecutor()
        // );
    }
    
    /// <summary>
    /// Registers a new runtime plugin with the registry.
    /// This is the ONLY place where new runtimes are registered - keeping it simple and explicit.
    /// </summary>
    /// <param name="runtimeType">The runtime type enum value</param>
    /// <param name="detector">Detector instance for this runtime</param>
    /// <param name="scanner">Scanner instance for this runtime</param>
    /// <param name="executorFactory">Factory to create executor instances</param>
    public static void RegisterRuntime(
        RuntimeType runtimeType,
        IRuntimeDetector detector,
        IRuntimeScanner scanner,
        Func<IRuntimeExecutor> executorFactory)
    {
        if (runtimeType == RuntimeType.Unknown || runtimeType == RuntimeType.Script)
        {
            throw new ArgumentException(
                $"Cannot register {runtimeType} - it is reserved", 
                nameof(runtimeType));
        }
        
        _detectors.Add(detector);
        
        if (_scanners.ContainsKey(runtimeType))
        {
            throw new InvalidOperationException(
                $"Runtime {runtimeType} is already registered");
        }
        
        _scanners[runtimeType] = scanner;
        _executorFactories[runtimeType] = executorFactory;
    }
    
    /// <summary>
    /// Detects the runtime type of a binary file.
    /// Iterates through all registered detectors until one reports it can handle the file.
    /// </summary>
    /// <param name="filePath">Absolute path to the binary file</param>
    /// <returns>Detected runtime type, or RuntimeType.Unknown if no detector matched</returns>
    public static RuntimeType DetectRuntime(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Binary file not found: {filePath}");
        }
        
        foreach (var detector in _detectors)
        {
            if (detector.CanHandle(filePath))
            {
                var detected = detector.Detect(filePath);
                if (detected != RuntimeType.Unknown)
                {
                    return detected;
                }
            }
        }
        
        return RuntimeType.Unknown;
    }
    
    /// <summary>
    /// Gets the actual managed assembly path for a binary.
    /// For native apphosts (.NET 5+ .exe files), returns the companion .dll path.
    /// For regular managed assemblies, returns the original path.
    /// </summary>
    /// <param name="filePath">Original file path (may be apphost .exe)</param>
    /// <returns>Path to the managed assembly (may be .dll if apphost was detected)</returns>
    public static string GetManagedAssemblyPath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Binary file not found: {filePath}");
        }
        
        foreach (var detector in _detectors)
        {
            if (detector.CanHandle(filePath))
            {
                // If it's a DotNetRuntimeDetector, check for apphost scenario
                if (detector is DotNetRuntimeDetector dotnetDetector)
                {
                    return dotnetDetector.GetManagedAssemblyPath(filePath);
                }
                
                // For other detectors, return original path
                return filePath;
            }
        }
        
        // If no detector can handle it, return original path
        return filePath;
    }
    
    /// <summary>
    /// Gets the scanner for a specific runtime type.
    /// </summary>
    /// <param name="runtimeType">The runtime type</param>
    /// <returns>Scanner instance</returns>
    /// <exception cref="NotSupportedException">If runtime is not registered</exception>
    public static IRuntimeScanner GetScanner(RuntimeType runtimeType)
    {
        if (!_scanners.TryGetValue(runtimeType, out var scanner))
        {
            throw new NotSupportedException(
                $"No scanner registered for runtime type: {runtimeType}. " +
                $"Ensure the runtime plugin is registered in RuntimeRegistry.");
        }
        
        return scanner;
    }
    
    /// <summary>
    /// Creates a new executor instance for a specific runtime type.
    /// Uses factory pattern to allow stateful executors.
    /// </summary>
    /// <param name="runtimeType">The runtime type</param>
    /// <returns>New executor instance</returns>
    /// <exception cref="NotSupportedException">If runtime is not registered</exception>
    public static IRuntimeExecutor CreateExecutor(RuntimeType runtimeType)
    {
        if (!_executorFactories.TryGetValue(runtimeType, out var factory))
        {
            throw new NotSupportedException(
                $"No executor registered for runtime type: {runtimeType}. " +
                $"Ensure the runtime plugin is registered in RuntimeRegistry.");
        }
        
        return factory();
    }
    
    /// <summary>
    /// Gets all registered runtime types (excluding Unknown and Script).
    /// Useful for UI to display available runtime import options.
    /// </summary>
    /// <returns>List of supported runtime types</returns>
    public static List<RuntimeType> GetSupportedRuntimes()
    {
        return _scanners.Keys.ToList();
    }
}



