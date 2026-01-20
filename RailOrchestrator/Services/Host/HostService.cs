// ============================================================================
// Rail HOST SERVICE - MINIMAL DESIGN
// ============================================================================
// SIMPLE: 
//   - 1 thread listens for connections
//   - Client calls Ignite() → connects → registered
//   - LLM calls Execute() → sends command to client → gets result
// NO POLLING, NO COMPLEXITY
// ============================================================================

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace WpfRagApp.Services.Host;

public sealed class HostService : IDisposable
{
    public const string PipeName = "RailHost";
    
    private readonly ConcurrentDictionary<string, ClientSession> _clients = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly AssetService _assetService; // Injected
    private Task? _listenerTask;
    private bool _disposed;
    
    public HostService(AssetService assetService) // Constructor injection
    {
        _assetService = assetService;
    }
    
    // ========================================================================
    // START/STOP
    // ========================================================================
    
    public void Start()
    {
        _listenerTask = Task.Run(ListenForConnections);
        System.Diagnostics.Debug.WriteLine("[HostService] Started");
    }
    
    public void Stop()
    {
        _cts.Cancel();
        foreach (var client in _clients.Values)
            client.Dispose();
        _clients.Clear();
    }
    
    // ========================================================================
    // LISTENER - Single thread, waits for connections
    // ========================================================================
    
    private async Task ListenForConnections()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                
                await pipe.WaitForConnectionAsync(_cts.Token);
                
                // Handle connection on separate task, continue listening
                _ = HandleNewClient(pipe);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HostService] Listen error: {ex.Message}");
            }
        }
    }
    
    private async Task HandleNewClient(NamedPipeServerStream pipe)
    {
        try
        {
            // Read first message to determine type
            var msg = await ReadMessage(pipe);
            if (msg == null) { pipe.Dispose(); return; }
            
            System.Diagnostics.Debug.WriteLine($"[HostService] Received message: {msg}");

            using var doc = JsonDocument.Parse(msg);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            
            if (type == "CONNECT")
            {
                // App registering with Ignite()
                await HandleConnect(pipe, root);
            }
            else if (type == "EXECUTE")
            {
                // RailFactory.Core proxy request
                await HandleProxyExecute(pipe, root);
            }
            else
            {
                pipe.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostService] Client error: {ex.Message}");
            pipe.Dispose();
        }
    }
    
    private async Task HandleConnect(NamedPipeServerStream pipe, JsonElement root)
    {
        var instanceId = root.GetProperty("instanceId").GetString() ?? "";
        var assembly = root.TryGetProperty("assembly", out var a) ? a.GetString() ?? "" : "";
        var manifest = root.GetProperty("manifest");
        
        // Parse language or default to "unknown"
        var language = manifest.TryGetProperty("language", out var l) ? l.GetString() ?? "unknown" : "unknown";
        
        var parsedFunctions = ParseFunctions(manifest);

        // AUTO-ASSOCIATION LOGIC:
        // If client sends no functions (typical for C# RailSDK), try to find a matching asset on disk
        if (parsedFunctions.Count == 0 && !string.IsNullOrEmpty(assembly))
        {
            try 
            {
                var assets = _assetService.GetAssets();
                // Match Assembly Name (e.g. "AgenticSiemensCNC") to Asset Internal Name (JSON) OR Folder Name
                // Using Case-Insensitive matching for robustness
                var matchedAsset = assets.FirstOrDefault(x => 
                    string.Equals(x.InternalName, assembly, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(x.Name, assembly, StringComparison.OrdinalIgnoreCase));
                
                if (matchedAsset != null && matchedAsset.Type == AssetType.Exe)
                {
                    var manifestPath = System.IO.Path.Combine(matchedAsset.Path, "Rail.manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        var jsonText = await File.ReadAllTextAsync(manifestPath);
                        using var loadedDoc = JsonDocument.Parse(jsonText);
                        var loadedFuncs = ParseFunctions(loadedDoc.RootElement);
                        
                        if (loadedFuncs.Count > 0)
                        {
                            parsedFunctions.AddRange(loadedFuncs);
                            System.Diagnostics.Debug.WriteLine($"[HostService] Auto-associated client '{assembly}' with manifest '{matchedAsset.Name}' (Internal: {matchedAsset.InternalName}) with {loadedFuncs.Count} functions");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"[HostService] Auto-association failed: {ex.Message}");
            }
        }

        var session = new ClientSession
        {
            InstanceId = instanceId,
            Assembly = assembly,
            Language = language.ToLower(),
            Pipe = pipe,
            Functions = parsedFunctions
        };
        
        _clients[instanceId] = session;
        
        await SendMessage(pipe, JsonSerializer.Serialize(new { type = "ACK" }));
        
        System.Diagnostics.Debug.WriteLine($"[HostService] Client connected: {instanceId} ({language})");
        
        await ClientMessageLoop(session);
    }
    
    // ========================================================================
    // ROUTING HELPERS
    // ========================================================================

    public ClientSession? FindClientForMethod(string methodName)
    {
        // 1. PRIORITY: Explicit Match (Any Language)
        // If a client explicitly says "I have this function", they win.
        foreach (var session in _clients.Values)
        {
            if (session.Functions.Any(f => f.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)))
            {
                return session;
            }
        }

        // 2. FALLBACK: Language-Specific Strategies
        foreach (var session in _clients.Values)
        {
            switch (session.Language)
            {
                case "csharp":
                    // C# Universal SDK supports Deep Reflection. 
                    // If method wasn't found explicitly above, we assume C# might have it via reflection.
                    return session;

                case "python":
                    // Future: Python might support dynamic lookup too. 
                    // For now, treat as explicit-only (fall through).
                    break;
                
                case "cpp":
                case "go":
                case "rust":
                case "nodejs":
                case "java":
                default:
                    // These languages MUST register methods explicitly in the manifest.
                    // If not found in Priority 1, they don't have it.
                    break;
            }
        }

        return null;
    }

    private async Task HandleProxyExecute(NamedPipeServerStream pipe, JsonElement root)
    {
        System.Diagnostics.Debug.WriteLine($"[HostService] HandleProxyExecute called. Clients count: {_clients.Count}");
        // RailFactory.Core wants to execute a function on a connected client
        var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetString() : Guid.NewGuid().ToString();
        var method = root.GetProperty("method").GetString() ?? "";
        var className = root.TryGetProperty("class", out var c) ? c.GetString() : null;
        var args = root.TryGetProperty("args", out var a) ? a : default;
        
        // Use smart routing to find the best client
        var session = FindClientForMethod(method);

        if (session == null)
        {
            var errResponse = JsonSerializer.Serialize(new { type = "RESULT", requestId, status = "error", message = "No suitable client found for method: " + method });
            await SendMessage(pipe, errResponse);
            pipe.Dispose();
            return;
        }
        
        try
        {
            // Forward to client with class info
            var execCmd = JsonSerializer.Serialize(new { type = "EXECUTE", requestId, method, @class = className, args });
            await SendMessage(session.Pipe, execCmd);
            
            // Wait for result from client
            var tcs = new TaskCompletionSource<string>();
            session.PendingRequests[requestId!] = tcs;
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await tcs.Task.WaitAsync(cts.Token);
            
            // Send result back to RailFactory.Core
            var response = JsonSerializer.Serialize(new { type = "RESULT", requestId, status = "success", result = JsonDocument.Parse(result).RootElement });
            await SendMessage(pipe, response);
        }
        catch (Exception ex)
        {
            var errResponse = JsonSerializer.Serialize(new { type = "RESULT", requestId, status = "error", message = ex.Message });
            await SendMessage(pipe, errResponse);
        }
        finally
        {
            pipe.Dispose();
        }
    }
    
    private async Task ClientMessageLoop(ClientSession session)
    {
        try
        {
            while (session.Pipe.IsConnected && !_cts.Token.IsCancellationRequested)
            {
                var msg = await ReadMessage(session.Pipe);
                if (msg == null) break;
                
                using var doc = JsonDocument.Parse(msg);
                var type = doc.RootElement.GetProperty("type").GetString();
                
                if (type == "RESULT")
                {
                    var requestId = doc.RootElement.GetProperty("requestId").GetString() ?? "";
                    if (session.PendingRequests.TryRemove(requestId, out var tcs))
                    {
                        tcs.SetResult(doc.RootElement.GetProperty("result").GetRawText());
                    }
                }
                else if (type == "DISCONNECT")
                {
                    break;
                }
            }
        }
        finally
        {
            _clients.TryRemove(session.InstanceId, out _);
            session.Dispose();
            System.Diagnostics.Debug.WriteLine($"[HostService] Client disconnected: {session.InstanceId}");
        }
    }
    
    // ========================================================================
    // EXECUTE - Called by LLM to run function on client
    // ========================================================================
    
    // ========================================================================
    // EXECUTE - Called by LLM to run function on client
    // ========================================================================
    
    public async Task<string> ExecuteAsync(string instanceId, string method, Dictionary<string, object?>? args = null)
    {
        if (!_clients.TryGetValue(instanceId, out var session))
            throw new Exception($"Client not found: {instanceId}");
        
        // Try to find class name from manifest info if available
        string? className = null;
        var funcInfo = session.Functions.FirstOrDefault(f => f.Name.Equals(method, StringComparison.OrdinalIgnoreCase));
        if (funcInfo != null)
        {
            className = funcInfo.ClassName;
        }

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>();
        session.PendingRequests[requestId] = tcs;
        
        var command = JsonSerializer.Serialize(new
        {
            type = "EXECUTE",
            requestId,
            method,
            @class = className, // Send class name to help client routing
            args = args ?? new Dictionary<string, object?>()
        });
        
        await SendMessage(session.Pipe, command);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await tcs.Task.WaitAsync(cts.Token);
    }
    
    public IReadOnlyDictionary<string, ClientSession> Clients => _clients;
    
    // ========================================================================
    // MESSAGE I/O
    // ========================================================================
    
    private static async Task SendMessage(PipeStream pipe, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var len = BitConverter.GetBytes(bytes.Length);
        await pipe.WriteAsync(len);
        await pipe.WriteAsync(bytes);
        await pipe.FlushAsync();
    }
    
    private static async Task<string?> ReadMessage(PipeStream pipe)
    {
        var lenBuf = new byte[4];
        if (await pipe.ReadAsync(lenBuf) < 4) return null;
        
        var len = BitConverter.ToInt32(lenBuf);
        if (len <= 0 || len > 1048576) return null; // Increased buffer safe limit to 1MB
        
        var buf = new byte[len];
        var read = 0;
        while (read < len)
        {
            var n = await pipe.ReadAsync(buf.AsMemory(read, len - read));
            if (n == 0) return null;
            read += n;
        }
        
        return Encoding.UTF8.GetString(buf);
    }
    
    private static List<FunctionInfo> ParseFunctions(JsonElement manifest)
    {
        var list = new List<FunctionInfo>();
        
        // Support both "tools" (new standard) and "functions" (legacy)
        JsonElement funcs;
        if (!manifest.TryGetProperty("tools", out funcs) && !manifest.TryGetProperty("functions", out funcs))
            return list;
        
        foreach (var f in funcs.EnumerateArray())
        {
            list.Add(new FunctionInfo
            {
                Name = f.GetProperty("name").GetString() ?? "",
                Description = f.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                ClassName = f.TryGetProperty("class", out var c) ? c.GetString() ?? "" : ""
            });
        }
        return list;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts.Dispose();
    }
}

// ========================================================================
// MODELS
// ========================================================================

public class ClientSession : IDisposable
{
    public string InstanceId { get; set; } = "";
    public string Assembly { get; set; } = "";
    public string Language { get; set; } = "unknown"; // "csharp", "cpp", "python", etc.
    public NamedPipeServerStream Pipe { get; set; } = null!;
    public List<FunctionInfo> Functions { get; set; } = new();
    public ConcurrentDictionary<string, TaskCompletionSource<string>> PendingRequests { get; } = new();
    
    public void Dispose() => Pipe?.Dispose();
}

public class FunctionInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ClassName { get; set; } = "";
}





