using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RailFactory.Core.TransportClients;

namespace RailFactory.Core;

/// <summary>
/// RailEngine provides a .NET wrapper for Rail Factory runtime execution.
/// 
/// Supports THREE modes:
/// 1. Script Mode: Python-based artifact execution (original)
/// 2. Binary Runtime Control Mode: IPC-based control of running .NET apps
/// 3. Composite Mode: Multi-module orchestration with automatic routing (NEW)
/// 
/// COMPOSITE MANIFEST SUPPORT:
/// When loading a composite manifest (manifest_type: "composite"), the engine:
/// - Creates a ModuleRegistry with lazy module instantiation
/// - Routes function calls using "Module.Function" addressing
/// - Auto-discovers module for unqualified function names
/// 
/// BACKWARD COMPATIBILITY:
/// Single manifests (v1) continue to work exactly as before.
/// </summary>
public class RailEngine : IDisposable
{
    private readonly string _artifactPath;
    private readonly string _manifestPath;
    
    // Single manifest mode (legacy)
    private JsonDocument? _manifestDoc;
    
    // Composite manifest mode (new)
    private CompositeManifest? _compositeManifest;
    private ModuleRegistry? _moduleRegistry;
    private bool _isComposite;
    
    private bool _isLoaded;
    private bool _isDisposed;

    /// <summary>
    /// Path to the artifact directory.
    /// </summary>
    public string ArtifactPath => _artifactPath;
    
    /// <summary>
    /// Path to the manifest file.
    /// </summary>
    public string ManifestPath => _manifestPath;
    
    /// <summary>
    /// Indicates if a composite manifest is loaded.
    /// </summary>
    public bool IsComposite => _isComposite;
    
    /// <summary>
    /// Module registry (only available for composite manifests).
    /// </summary>
    public ModuleRegistry? Registry => _moduleRegistry;

    /// <summary>
    /// Initializes a new instance of the RailEngine class.
    /// </summary>
    /// <param name="artifactPath">The path to the Rail artifact directory (containing Rail.manifest.json)</param>
    public RailEngine(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
            throw new ArgumentException("Artifact path cannot be null or empty.", nameof(artifactPath));

        if (!Directory.Exists(artifactPath))
            throw new DirectoryNotFoundException($"Artifact directory not found: {artifactPath}");

        var manifestPath = Path.Combine(artifactPath, "Rail.manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Manifest file not found: {manifestPath}");

        _artifactPath = artifactPath;
        _manifestPath = manifestPath;
    }

    /// <summary>
    /// Gets runtime.py path by extracting from embedded resources.
    /// Enterprise-grade: No external files needed, everything embedded in DLL.
    /// </summary>
    private string GetRuntimePath()
    {
        try
        {
            return EmbeddedResourceExtractor.ExtractRuntime();
        }
        catch (Exception ex)
        {
            throw new FileNotFoundException(
                "Failed to extract embedded Python runtime. " +
                "Ensure RailFactory.Core.dll is properly deployed with all embedded resources.",
                ex);
        }
    }

    /// <summary>
    /// Loads the manifest and returns JSON for LLM.
    /// Auto-detects manifest type (single vs composite).
    /// 
    /// For composite manifests, returns a flattened tools array with module prefixes.
    /// </summary>
    public string Load()
    {
        ThrowIfDisposed();
        
        try
        {
            var manifestJson = File.ReadAllText(_manifestPath);
            
            // Detect manifest type
            if (IsCompositeManifest(manifestJson))
            {
                return LoadCompositeManifest(manifestJson);
            }
            else
            {
                return LoadSingleManifest(manifestJson);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid JSON in manifest file: {_manifestPath}", ex);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new IOException($"Failed to load manifest from: {_manifestPath}", ex);
        }
    }
    
    /// <summary>
    /// Loads a single (legacy) manifest.
    /// </summary>
    private string LoadSingleManifest(string manifestJson)
    {
        _manifestDoc = JsonDocument.Parse(manifestJson);
        _isComposite = false;
        _isLoaded = true;
        return manifestJson;
    }
    
    /// <summary>
    /// Loads a composite manifest and creates the module registry.
    /// </summary>
    private string LoadCompositeManifest(string manifestJson)
    {
        _compositeManifest = JsonSerializer.Deserialize<CompositeManifest>(manifestJson);
        
        if (_compositeManifest == null)
            throw new InvalidDataException("Failed to parse composite manifest");
        
        _moduleRegistry = new ModuleRegistry(_compositeManifest, _artifactPath);
        _isComposite = true;
        _isLoaded = true;
        
        // Return flattened tools for LLM
        return SerializeToolsForLLM();
    }
    
    /// <summary>
    /// Detects if a manifest is composite (v2.0) or single (v1.0).
    /// </summary>
    private static bool IsCompositeManifest(string manifestJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(manifestJson);
            var root = doc.RootElement;
            
            // Check for manifest_type: "composite"
            if (root.TryGetProperty("manifest_type", out var manifestType))
            {
                return manifestType.GetString()?.Equals("composite", StringComparison.OrdinalIgnoreCase) == true;
            }
            
            // Check for modules array (alternative detection)
            if (root.TryGetProperty("modules", out var modules) && modules.ValueKind == JsonValueKind.Array)
            {
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Serializes all tools from composite manifest into flat array for LLM.
    /// Tool names are encoded for Gemini API compatibility: "ModuleId__FunctionName"
    /// 
    /// ENTERPRISE DESIGN:
    /// - Uses FunctionNameEncoder for reversible name encoding
    /// - Includes 'class' field for internal routing (removed before sending to Gemini)
    /// - Preserves all original tool metadata
    /// </summary>
    private string SerializeToolsForLLM()
    {
        if (_compositeManifest == null)
            throw new InvalidOperationException("Composite manifest not loaded");
        
        var allTools = new List<object>();
        
        foreach (var module in _compositeManifest.Modules)
        {
            foreach (var tool in module.Tools)
            {
                // Create qualified name: "ModuleId.FunctionName"
                var qualifiedName = $"{module.ModuleId}.{tool.Name}";
                
                // Encode for Gemini API compatibility (dots not allowed)
                var encodedName = FunctionNameEncoder.Encode(qualifiedName);
                
                // Create tool with encoded name and class for routing
                var prefixedTool = new Dictionary<string, object>
                {
                    ["name"] = encodedName,
                    ["description"] = $"[{module.ModuleId}] {tool.Description}",
                    ["class"] = tool.ClassName,  // For internal routing (removed by CleanToolsForLLM)
                    ["parameters"] = tool.Parameters
                };
                
                allTools.Add(prefixedTool);
            }
        }
        
        var result = new Dictionary<string, object>
        {
            ["version"] = _compositeManifest.Version,
            ["manifest_type"] = "composite",
            ["solution_name"] = _compositeManifest.SolutionName,
            ["tools"] = allTools
        };
        
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Executes a function from the Rail artifact.
    /// 
    /// For single manifests:
    /// - Auto-detects runtime_type and routes to appropriate executor
    /// 
    /// For composite manifests:
    /// - Supports "Module.Function" addressing
    /// - Auto-discovers module for unqualified function names
    /// </summary>
    public string Execute(string functionName, string argsJson)
    {
        ThrowIfDisposed();
        
        if (!_isLoaded)
            throw new InvalidOperationException("Manifest not loaded. Call Load() first.");
        
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function name cannot be null or empty", nameof(functionName));

        // Allow empty args (will default to {})
        argsJson = string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson;

        // Validate argsJson
        try
        {
            JsonDocument.Parse(argsJson);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Invalid JSON in arguments", nameof(argsJson), ex);
        }

        // Route to appropriate handler
        if (_isComposite)
        {
            return ExecuteComposite(functionName, argsJson);
        }
        else
        {
            return ExecuteSingle(functionName, argsJson);
        }
    }
    
    /// <summary>
    /// Executes function on composite manifest using module routing.
    /// </summary>
    private string ExecuteComposite(string functionName, string argsJson)
    {
        if (_moduleRegistry == null)
            throw new InvalidOperationException("Module registry not initialized");
        
        // Parse "Module.Function" addressing
        var (moduleId, funcName) = ParseFunctionAddress(functionName);
        
        // If no module specified, try to auto-discover
        if (string.IsNullOrEmpty(moduleId))
        {
            moduleId = _moduleRegistry.FindModuleForFunction(funcName);
            
            if (string.IsNullOrEmpty(moduleId))
            {
                throw new InvalidOperationException(
                    $"Function '{functionName}' not found in any module. " +
                    $"Use qualified name format: ModuleId.FunctionName. " +
                    $"Available modules: {string.Join(", ", _moduleRegistry.ModuleIds)}");
            }
        }
        
        // Get or create module instance (lazy init)
        var module = _moduleRegistry.GetOrCreate(moduleId);
        
        // Execute on module
        return module.Execute(funcName, argsJson);
    }
    
    /// <summary>
    /// Parses function address into module ID and function name.
    /// Supports:
    /// - "Module.Function" → ("Module", "Function")
    /// - "Function" → (null, "Function")
    /// </summary>
    private static (string? moduleId, string funcName) ParseFunctionAddress(string functionName)
    {
        var dotIndex = functionName.IndexOf('.');
        
        if (dotIndex > 0 && dotIndex < functionName.Length - 1)
        {
            return (
                functionName.Substring(0, dotIndex),
                functionName.Substring(dotIndex + 1)
            );
        }
        
        return (null, functionName);
    }
    
    /// <summary>
    /// Executes function on single (legacy) manifest.
    /// </summary>
    private string ExecuteSingle(string functionName, string argsJson)
    {
        var runtimeType = GetRuntimeType().ToLowerInvariant();

        switch (runtimeType)
        {
            case "dotnetbinary":
            case "dotnet-ipc":
                return ExecuteViaBinaryIpc(functionName, argsJson);

            case "native":
            case "native-bridge":
            case "cpp": // Legacy tolerance
                // Native apps cannot be launched by RailEngine (offline mode).
                // They must be connected via HostService (online mode).
                throw new InvalidOperationException(
                    $"The function '{functionName}' belongs to a Native Application ('{runtimeType}') which is not currently connected.\n" +
                    "Please ensure the target application is running and connected to the Rail Host.");

            case "generative_powershell":
                return ExecuteViaPowerShell(functionName, argsJson);

            case "script":
            case "python-script":
            default:
                return ExecuteViaScript(functionName, argsJson);
        }
    }

    /// <summary>
    /// Gets runtime_type from loaded manifest.
    /// </summary>
    private string GetRuntimeType()
    {
        if (_manifestDoc == null)
            throw new InvalidOperationException("Manifest not loaded. Call Load() first.");

        if (_manifestDoc.RootElement.TryGetProperty("runtime_type", out var runtimeTypeElement))
        {
            return runtimeTypeElement.GetString() ?? "script";
        }

        return "script"; // Default to script mode
    }

    /// <summary>
    /// Executes via Python runtime (Script mode).
    /// </summary>
    private string ExecuteViaScript(string functionName, string argsJson)
    {
        var runtimePath = GetRuntimePath();
        var escapedArgs = argsJson.Replace("\"", "\\\"");

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{runtimePath}\" --artifact \"{_artifactPath}\" --func \"{functionName}\" --args \"{escapedArgs}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

        try
        {
            using var process = Process.Start(processStartInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start Python process");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Python execution failed with exit code {process.ExitCode}. Error: {error}");
            }

            return output;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to execute function '{functionName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a dynamic PowerShell script (Generative Automation).
    /// </summary>
    private string ExecuteViaPowerShell(string functionName, string argsJson)
    {
        // For generative powershell, the "function" is always "Execute"
        // and the args contains the "script".
        
        // Basic check
        if (!functionName.Contains("Execute", StringComparison.OrdinalIgnoreCase))
        {
             // Allow 'PowerShell.Execute' or just 'Execute'
        }

        string script = "";
        try 
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.TryGetProperty("script", out var scriptElement))
            {
                script = scriptElement.GetString() ?? "";
            }
        }
        catch
        {
             // Fallback: assume the whole body is the script if parsing fails? 
             // No, unsafe. Be strict.
             throw new ArgumentException("Invalid JSON arguments. Expected {\"script\": \"...\"}");
        }
        
        if (string.IsNullOrWhiteSpace(script))
            return "{\"status\": \"noop\", \"message\": \"Empty script\"}";

        // Encode script to Base64 to avoid CLI escaping limits
        var scriptBytes = Encoding.Unicode.GetBytes(script);
        var encodedScript = Convert.ToBase64String(scriptBytes);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            // -EncodedCommand expects Base64 UTF-16LE
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
            UseShellExecute = false,
            CreateNoWindow = true, // HIDDEN EXECUTION
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = processStartInfo };
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        try 
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            // Wait with timeout (e.g. 60 seconds)
            if (!process.WaitForExit(60000)) 
            {
                process.Kill();
                return "{\"status\": \"error\", \"message\": \"Timeout: Script execution took too long (>60s)\"}";
            }

            if (process.ExitCode != 0)
            {
                return $"{{\"status\": \"error\", \"exit_code\": {process.ExitCode}, \"message\": {JsonSerializer.Serialize(errorBuilder.ToString())}}}";
            }

            return $"{{\"status\": \"success\", \"output\": {JsonSerializer.Serialize(outputBuilder.ToString())}}}";
        }
        catch (Exception ex)
        {
            return $"{{\"status\": \"error\", \"message\": {JsonSerializer.Serialize(ex.Message)}}}";
        }
    }

    /// <summary>
    /// Executes via IPC to running binary (Binary Runtime Control mode).
    /// Uses universal RailHost pipe for v2.0 architecture.
    /// </summary>
    private string ExecuteViaBinaryIpc(string functionName, string argsJson)
    {
        // v2.0: Use universal RailHost pipe
        const string pipeName = "RailHost";
        
        // Lookup class from manifest for this function
        var className = GetFunctionClass(functionName);

        return ExecuteViaNamedPipe(pipeName, functionName, className, argsJson);
    }
    
    /// <summary>
    /// Looks up the class name for a function from the manifest.
    /// Returns null if not found or not specified.
    /// </summary>
    private string? GetFunctionClass(string functionName)
    {
        if (_manifestDoc == null) return null;
        
        var root = _manifestDoc.RootElement;
        
        // Check "tools" array (standard manifest format)
        if (root.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array)
        {
            foreach (var tool in tools.EnumerateArray())
            {
                if (tool.TryGetProperty("name", out var nameProp) && 
                    nameProp.GetString() == functionName &&
                    tool.TryGetProperty("class", out var classProp))
                {
                    return classProp.GetString();
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Sends command to HostService via Named Pipe using v2.0 protocol.
    /// Protocol: length-prefix (4 bytes) + JSON message
    /// </summary>
    private string ExecuteViaNamedPipe(string pipeName, string functionName, string? className, string argsJson)
    {
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            
            pipeClient.Connect(5000);

            // v2.0 EXECUTE protocol with class for reflection lookup
            var command = new
            {
                type = "EXECUTE",
                requestId = Guid.NewGuid().ToString(),
                method = functionName,
                @class = className,
                args = JsonDocument.Parse(argsJson).RootElement
            };

            var commandJson = JsonSerializer.Serialize(command);
            
            // Send with length prefix
            var bytes = Encoding.UTF8.GetBytes(commandJson);
            var lengthBytes = BitConverter.GetBytes(bytes.Length);
            pipeClient.Write(lengthBytes, 0, 4);
            pipeClient.Write(bytes, 0, bytes.Length);
            pipeClient.Flush();

            // Read response with length prefix
            var lenBuffer = new byte[4];
            if (pipeClient.Read(lenBuffer, 0, 4) < 4)
                throw new InvalidOperationException("Failed to read response length");
            
            var responseLen = BitConverter.ToInt32(lenBuffer, 0);
            if (responseLen <= 0 || responseLen > 65536)
                throw new InvalidOperationException("Invalid response length");
            
            var responseBuffer = new byte[responseLen];
            var totalRead = 0;
            while (totalRead < responseLen)
            {
                var read = pipeClient.Read(responseBuffer, totalRead, responseLen - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
            
            var responseJson = Encoding.UTF8.GetString(responseBuffer, 0, totalRead);
            
            // Parse v2.0 RESULT format and convert to legacy format
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("result", out var resultProp))
            {
                // Convert to legacy response format
                return JsonSerializer.Serialize(new { status = "success", result = resultProp });
            }
            else if (root.TryGetProperty("message", out var msgProp))
            {
                return JsonSerializer.Serialize(new { status = "error", error = msgProp.GetString() });
            }
            
            return responseJson;
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException(
                $"Timeout connecting to RailHost. Ensure the target application is running with RailEngine.Ignite() called.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to execute via IPC: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Throws if engine has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(RailEngine));
    }
    
    /// <summary>
    /// Disposes all resources, including module registry and connections.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;
        
        _moduleRegistry?.Dispose();
        _manifestDoc?.Dispose();
        
        _isDisposed = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // RUNTIME CONTROL MODE - Ignite API (for target applications)
    // ═══════════════════════════════════════════════════════════════

    private static RailIpcServer? _ipcServer;
    private static SingleInstanceManager? _singleInstanceManager;

    /// <summary>
    /// Event raised when a function is about to be called via IPC.
    /// Subscribe to this in your App.xaml.cs to implement UI highlighting.
    /// </summary>
    public static event Action<Events.FunctionCallEvent>? OnFunctionCalling;

    /// <summary>
    /// Enables LLM-based runtime control of your application.
    /// Call ONCE in App.xaml.cs OnStartup() method.
    /// 
    /// Enforces single instance and starts IPC server for remote control.
    /// </summary>
    /// <param name="appInstance">The application instance (e.g., this from App class)</param>
    public static void Ignite(object appInstance)
    {
        if (appInstance == null)
            throw new ArgumentNullException(nameof(appInstance));

        var appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "RailApp";

        _singleInstanceManager = new SingleInstanceManager(appName);

        if (!_singleInstanceManager.IsFirstInstance())
        {
            Environment.Exit(0);
            return;
        }

        var pipeName = $"RailEngine_{DeterministicHash.GetHash(appName)}";
        var executor = RuntimeRegistry.CreateExecutor(RuntimeType.DotNetBinary);
        
        _ipcServer = new RailIpcServer(pipeName, executor);
        
        // Connect IPC events to static event for UI highlighting
        _ipcServer.OnFunctionCalling += (evt) => OnFunctionCalling?.Invoke(evt);
        
        _ipcServer.StartServer(appInstance);

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        try
        {
            _ipcServer?.Stop();
            _singleInstanceManager?.Dispose();
        }
        catch
        {
            // Suppress exceptions during cleanup
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // IGNITE OPTIONS & HOST MODE SUPPORT
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Configuration options for the Ignite method.
    /// </summary>
    public class IgniteOptions
    {
        /// <summary>
        /// If true, connect to Rail Host service instead of running standalone IPC.
        /// Default is false (legacy mode for backward compatibility).
        /// </summary>
        public bool UseHostMode { get; set; } = false;

        /// <summary>
        /// Optional context name for identifying this application in Host mode.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// If true, include private methods (starting with _) in discovery.
        /// Default is false.
        /// </summary>
        public bool IncludePrivateMethods { get; set; } = false;
    }

    /// <summary>
    /// Enables LLM-based runtime control of your application with configuration options.
    /// Use this overload to enable Host mode or customize behavior.
    /// </summary>
    /// <param name="appInstance">The application instance</param>
    /// <param name="options">Configuration options</param>
    public static void Ignite(object appInstance, IgniteOptions options)
    {
        if (options.UseHostMode)
        {
            IgniteHostMode(appInstance, options);
        }
        else
        {
            // Use legacy mode
            Ignite(appInstance);
        }
    }

    private static void IgniteHostMode(object appInstance, IgniteOptions options)
    {
        if (appInstance == null)
            throw new ArgumentNullException(nameof(appInstance));

        // TODO: Implement Host mode connection
        // This will use the RailBridge.dll to connect to HostService
        // For now, fallback to legacy mode with a warning
        System.Diagnostics.Debug.WriteLine("[RailEngine] Host mode not yet fully implemented, using legacy mode");
        Ignite(appInstance);
    }
}



