// ============================================================================
// BRIDGE STATE - CONNECTION AND LISTENER MANAGEMENT
// ============================================================================
// Manages the Named Pipe connection to Rail Host and the command listener
// thread. Implements IDisposable for proper cleanup.
//
// THREAD SAFETY:
//   - Connect/Disconnect are synchronized
//   - Listener runs on dedicated background thread
//   - Callback invocations are serialized per command
//
// ============================================================================

using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using static RailBridge.Native.Exports;

namespace RailBridge.Native;

/// <summary>
/// Manages the connection state between Bridge and Host.
/// </summary>
internal sealed class BridgeState : IDisposable
{
    // ========================================================================
    // CONFIGURATION
    // ========================================================================
    
    private const string HostPipeName = "RailHost";
    private const int ConnectTimeoutMs = 5000;
    private const int ReadBufferSize = 65536;
    
    // ========================================================================
    // STATE
    // ========================================================================
    
    private readonly string _instanceId;
    private readonly string _manifest;
    private readonly RailCommandCallback _callback;
    
    private NamedPipeClientStream? _pipe;
    private Thread? _listenerThread;
    private CancellationTokenSource? _cts;
    private volatile bool _isConnected;
    private bool _disposed;
    
    /// <summary>
    /// True if currently connected to the Host.
    /// </summary>
    public bool IsConnected => _isConnected && _pipe?.IsConnected == true;
    
    // ========================================================================
    // CONSTRUCTOR
    // ========================================================================
    
    public BridgeState(string instanceId, string manifest, RailCommandCallback callback)
    {
        _instanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }
    
    // ========================================================================
    // CONNECTION
    // ========================================================================
    
    /// <summary>
    /// Connect to the Host and start the listener thread.
    /// </summary>
    public int Connect()
    {
        try
        {
            // Create pipe client
            _pipe = new NamedPipeClientStream(
                serverName: ".",
                pipeName: HostPipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);
            
            // Connect with timeout
            _pipe.Connect(ConnectTimeoutMs);
            
            if (!_pipe.IsConnected)
                return ErrorCodes.ConnectionFailed;
            
            // Send handshake
            var handshakeResult = SendHandshake();
            if (handshakeResult != ErrorCodes.Success)
                return handshakeResult;
            
            // Start listener thread
            _cts = new CancellationTokenSource();
            _listenerThread = new Thread(ListenerLoop)
            {
                Name = "RailBridge-Listener",
                IsBackground = true
            };
            _listenerThread.Start();
            
            _isConnected = true;
            return ErrorCodes.Success;
        }
        catch (TimeoutException)
        {
            return ErrorCodes.Timeout;
        }
        catch (IOException)
        {
            return ErrorCodes.ConnectionFailed;
        }
        catch
        {
            return ErrorCodes.UnknownError;
        }
    }
    
    /// <summary>
    /// Send a heartbeat to the Host.
    /// </summary>
    public int SendHeartbeat()
    {
        try
        {
            var message = new ProtocolMessage
            {
                Type = "HEARTBEAT",
                InstanceId = _instanceId
            };
            
            SendMessage(message, BridgeJsonContext.Default.ProtocolMessage);
            return ErrorCodes.Success;
        }
        catch
        {
            return ErrorCodes.PipeBroken;
        }
    }
    
    // ========================================================================
    // HANDSHAKE
    // ========================================================================
    
    private int SendHandshake()
    {
        try
        {
            // Build CONNECT message
            var connectMessage = new ConnectMessage
            {
                Type = "CONNECT",
                InstanceId = _instanceId,
                ProcessId = Environment.ProcessId,
                Assembly = _instanceId,
                Manifest = JsonDocument.Parse(_manifest).RootElement
            };
            
            var json = JsonSerializer.Serialize(connectMessage, BridgeJsonContext.Default.ConnectMessage);
            WriteMessage(json);
            
            // Wait for ACK
            var response = ReadMessage();
            if (string.IsNullOrEmpty(response))
                return ErrorCodes.ConnectionFailed;
            
            using var doc = JsonDocument.Parse(response);
            var type = doc.RootElement.GetProperty("type").GetString();
            
            if (type != "ACK")
                return ErrorCodes.ConnectionFailed;
            
            return ErrorCodes.Success;
        }
        catch
        {
            return ErrorCodes.ConnectionFailed;
        }
    }
    
    // ========================================================================
    // LISTENER LOOP
    // ========================================================================
    
    private void ListenerLoop()
    {
        var token = _cts?.Token ?? CancellationToken.None;
        
        while (!token.IsCancellationRequested && _pipe?.IsConnected == true)
        {
            try
            {
                var message = ReadMessage();
                if (string.IsNullOrEmpty(message))
                {
                    // Pipe disconnected or error - exit loop
                    _isConnected = false;
                    break;
                }
                
                ProcessMessage(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // Pipe disconnected
                _isConnected = false;
                break;
            }
            catch
            {
                // Log error but continue listening
                System.Diagnostics.Debug.WriteLine("[RailBridge] Error processing message");
            }
        }
        
        _isConnected = false;
    }
    
    private void ProcessMessage(string messageJson)
    {
        using var doc = JsonDocument.Parse(messageJson);
        var root = doc.RootElement;
        
        var type = root.GetProperty("type").GetString();
        
        switch (type)
        {
            case "EXECUTE":
                HandleExecute(root);
                break;
                
            case "PING":
                SendPong();
                break;
                
            case "DISCONNECT":
                _isConnected = false;
                break;
        }
    }
    
    private void HandleExecute(JsonElement message)
    {
        var requestId = message.GetProperty("requestId").GetString() ?? "";
        var method = message.GetProperty("method").GetString() ?? "";
        var argsJson = message.TryGetProperty("args", out var argsElement) 
            ? argsElement.GetRawText() 
            : "{}";
        
        // Build command JSON for callback - pass raw args directly
        // The SDK will handle parsing in its native language
        var commandJson = $"{{\"method\":\"{method}\",\"args\":{argsJson}}}";
        
        // Invoke callback
        string resultJson;
        try
        {
            var commandPtr = Marshal.StringToCoTaskMemUTF8(commandJson);
            var resultPtr = _callback(commandPtr);
            Marshal.FreeCoTaskMem(commandPtr);
            
            resultJson = Marshal.PtrToStringUTF8(resultPtr) ?? "{}";
        }
        catch (Exception ex)
        {
            var errorResult = new CallbackResult
            {
                Status = "error",
                Message = ex.Message
            };
            resultJson = JsonSerializer.Serialize(errorResult, BridgeJsonContext.Default.CallbackResult);
        }
        
        // Send result back to Host
        var response = new ResultMessage
        {
            Type = "RESULT",
            RequestId = requestId,
            Result = JsonDocument.Parse(resultJson).RootElement
        };
        
        SendMessage(response, BridgeJsonContext.Default.ResultMessage);
    }
    
    private void SendPong()
    {
        var pong = new ProtocolMessage
        {
            Type = "PONG",
            InstanceId = _instanceId
        };
        SendMessage(pong, BridgeJsonContext.Default.ProtocolMessage);
    }
    
    // ========================================================================
    // MESSAGE I/O
    // ========================================================================
    
    private void SendMessage<T>(T message, JsonTypeInfo<T> typeInfo) where T : notnull
    {
        var json = JsonSerializer.Serialize(message, typeInfo);
        WriteMessage(json);
    }
    
    private void WriteMessage(string message)
    {
        if (_pipe == null || !_pipe.IsConnected)
            throw new IOException("Pipe not connected");
        
        var bytes = Encoding.UTF8.GetBytes(message);
        var lengthBytes = BitConverter.GetBytes(bytes.Length);
        
        _pipe.Write(lengthBytes, 0, 4);
        _pipe.Write(bytes, 0, bytes.Length);
        _pipe.Flush();
    }
    
    private string? ReadMessage()
    {
        if (_pipe == null || !_pipe.IsConnected)
            return null;
        
        // Read length prefix (4 bytes)
        var lengthBytes = new byte[4];
        var bytesRead = _pipe.Read(lengthBytes, 0, 4);
        if (bytesRead < 4)
            return null;
        
        var length = BitConverter.ToInt32(lengthBytes, 0);
        if (length <= 0 || length > ReadBufferSize)
            return null;
        
        // Read message body
        var buffer = new byte[length];
        var totalRead = 0;
        while (totalRead < length)
        {
            var read = _pipe.Read(buffer, totalRead, length - totalRead);
            if (read == 0)
                return null;
            totalRead += read;
        }
        
        return Encoding.UTF8.GetString(buffer);
    }
    
    // ========================================================================
    // DISPOSAL
    // ========================================================================
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        _isConnected = false;
        
        // Signal listener to stop
        _cts?.Cancel();
        
        // Send disconnect message (best effort)
        try
        {
            if (_pipe?.IsConnected == true)
            {
                var disconnectMsg = new ProtocolMessage
                {
                    Type = "DISCONNECT",
                    InstanceId = _instanceId
                };
                SendMessage(disconnectMsg, BridgeJsonContext.Default.ProtocolMessage);
            }
        }
        catch { /* Ignore errors during shutdown */ }
        
        // Wait for listener thread
        _listenerThread?.Join(TimeSpan.FromSeconds(1));
        
        // Cleanup
        _pipe?.Dispose();
        _cts?.Dispose();
    }
}

// ============================================================================
// PROTOCOL MESSAGES
// ============================================================================

internal class ProtocolMessage
{
    public string Type { get; set; } = "";
    public string InstanceId { get; set; } = "";
}

internal class ConnectMessage : ProtocolMessage
{
    public int ProcessId { get; set; }
    public string Assembly { get; set; } = "";
    public JsonElement Manifest { get; set; }
}

internal class ResultMessage
{
    public string Type { get; set; } = "";
    public string RequestId { get; set; } = "";
    public JsonElement Result { get; set; }
}

// ============================================================================
// SERIALIZER OPTIONS
// ============================================================================

internal static class SerializerOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}



