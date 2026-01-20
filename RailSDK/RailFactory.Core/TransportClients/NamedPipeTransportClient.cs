using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RailFactory.Core.TransportClients;

/// <summary>
/// Transport client for .NET binary modules using Named Pipes IPC.
/// 
/// PROTOCOL:
/// - Pipe name: "RailEngine_{hash(moduleName)}"
/// - Message format: JSON command/response
/// - Connection: Per-request (no persistent connection)
/// 
/// REQUIREMENTS:
/// - Target application must call RailEngine.Ignite() on startup
/// - Target application must be running before Execute() is called
/// </summary>
public class NamedPipeTransportClient : ITransportClient
{
    private ModuleManifest? _module;
    private string _basePath = string.Empty;
    private string _pipeName = string.Empty;
    private bool _isInitialized;
    
    /// <summary>
    /// Connection timeout in milliseconds.
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// Read buffer size in bytes.
    /// </summary>
    public int BufferSize { get; set; } = 65536; // 64KB for large responses
    
    public string TransportType => "namedpipe";
    
    public bool IsConnected => _isInitialized;
    
    public void Initialize(ModuleManifest module, string basePath)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        
        // v2.0: Use universal HostService pipe
        _pipeName = "RailHost";
        
        _isInitialized = true;
    }
    
    public string Execute(string functionName, string argsJson)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Transport not initialized. Call Initialize() first.");
        
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function name cannot be null or empty", nameof(functionName));
        
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            
            // Connect with timeout
            pipeClient.Connect(ConnectionTimeoutMs);
            
            // Lookup class from manifest for disambiguation
            var className = GetClassForFunction(functionName);
            
            // Build v2.0 EXECUTE command
            var command = new
            {
                type = "EXECUTE",
                requestId = Guid.NewGuid().ToString(),
                method = functionName,
                @class = className,
                args = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson).RootElement
            };
            
            var commandJson = JsonSerializer.Serialize(command);
            var buffer = Encoding.UTF8.GetBytes(commandJson);
            var lenBytes = BitConverter.GetBytes(buffer.Length);
            
            // Send command with length prefix
            pipeClient.Write(lenBytes, 0, 4);
            pipeClient.Write(buffer, 0, buffer.Length);
            pipeClient.Flush();
            
            // Read response length
            var lenBuf = new byte[4];
            if (pipeClient.Read(lenBuf, 0, 4) < 4)
                throw new InvalidOperationException("Failed to read response length");
            
            var responseLen = BitConverter.ToInt32(lenBuf, 0);
            if (responseLen <= 0 || responseLen > 1048576) // 1MB max
                throw new InvalidOperationException($"Invalid response length: {responseLen}");
            
            // Read response
            var responseBuffer = new byte[responseLen];
            var totalRead = 0;
            while (totalRead < responseLen)
            {
                var read = pipeClient.Read(responseBuffer, totalRead, responseLen - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
            
            var responseJson = Encoding.UTF8.GetString(responseBuffer, 0, totalRead);
            
            // Parse v2.0 RESULT format and convert to legacy format expected by caller
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
            else if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "error") 
            {
                 // Handle error response from HostService directly
                 return JsonSerializer.Serialize(new { status = "error", error = root.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error" });
            }
            
            return responseJson;
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException(
                "Timeout connecting to RailHost. Ensure RailLLM HostService is running.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to execute via IPC: {ex.Message}", ex);
        }
    }
    
    public bool Ping()
    {
        if (!_isInitialized) return false;
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            pipeClient.Connect(100); 
            return true;
        }
        catch { return false; }
    }
    
    public void Dispose()
    {
        // No persistent resources for Named Pipe client
        // Each Execute() creates its own connection
        _isInitialized = false;
    }
    
    /// <summary>
    /// Looks up the class name for a function from the module manifest.
    /// Returns null if not found.
    /// </summary>
    /// <param name="functionName">Name of the function to look up</param>
    /// <returns>Fully qualified class name, or null</returns>
    private string? GetClassForFunction(string functionName)
    {
        if (_module?.Tools == null)
            return null;
        
        // Find the tool by name and return its class
        var tool = _module.Tools.FirstOrDefault(t => 
            string.Equals(t.Name, functionName, StringComparison.OrdinalIgnoreCase));
        
        return tool?.ClassName;
    }
}



