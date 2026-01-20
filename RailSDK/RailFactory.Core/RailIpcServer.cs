using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RailFactory.Core.Events;

namespace RailFactory.Core;

/// <summary>
/// Manages Named Pipe IPC server for receiving commands from external processes.
/// Runs on a background thread to avoid blocking the UI.
/// </summary>
public class RailIpcServer : IRuntimeIpcProvider
{
    private NamedPipeServerStream? _pipeServer;
    private readonly string _pipeName;
    private object? _appInstance;
    private readonly IRuntimeExecutor _executor;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Event raised when a function is about to be called.
    /// Used for UI highlighting/workflow visualization.
    /// </summary>
    public event Action<FunctionCallEvent>? OnFunctionCalling;

    public RailIpcServer(string pipeName, IRuntimeExecutor executor)
    {
        _pipeName = pipeName;
        _executor = executor;
    }

    public void StartServer(object appInstance)
    {
        _appInstance = appInstance;
        _executor.Initialize(appInstance);
        _cancellationTokenSource = new CancellationTokenSource();

        // Start listening on background thread
        Task.Run(() => ListenForCommands(_cancellationTokenSource.Token));
    }

    private async Task ListenForCommands(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                // Wait for connection (with cancellation support)
                await _pipeServer.WaitForConnectionAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine("[RailIPC] Client connected");

                // Read command JSON
                var commandJson = await ReadMessageAsync(_pipeServer);
                System.Diagnostics.Debug.WriteLine($"[RailIPC] Received: {commandJson?.Substring(0, Math.Min(commandJson?.Length ?? 0, 200))}...");
                
                if (string.IsNullOrEmpty(commandJson))
                    continue;

                // Deserialize command
                var command = JsonConvert.DeserializeObject<RailCommand>(commandJson);
                if (command == null)
                {
                    System.Diagnostics.Debug.WriteLine("[RailIPC] Failed to deserialize command");
                    continue;
                }
                System.Diagnostics.Debug.WriteLine($"[RailIPC] Command: {command.MethodName}");

                // Execute command via executor
                object? result = null;
                string errorMessage = string.Empty;

                // Emit "before" event for UI highlighting
                var callEvent = new FunctionCallEvent
                {
                    FunctionName = command.MethodName,
                    Parameters = command.Arguments?.ToObject<Dictionary<string, object?>>() 
                                 ?? new Dictionary<string, object?>(),
                    Phase = "before",
                    Timestamp = DateTime.Now
                };
                
                try
                {
                    OnFunctionCalling?.Invoke(callEvent);
                }
                catch { /* Don't let UI errors break execution */ }

                try
                {
                    result = _executor.Execute(command);
                    System.Diagnostics.Debug.WriteLine($"[RailIPC] Execution success, result type: {result?.GetType().Name}");
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    System.Diagnostics.Debug.WriteLine($"[RailIPC] Execution error: {errorMessage}");
                }

                // Send response
                var response = new
                {
                    status = string.IsNullOrEmpty(errorMessage) ? "success" : "error",
                    result = result,
                    error = errorMessage
                };

                var responseJson = JsonConvert.SerializeObject(response);
                System.Diagnostics.Debug.WriteLine($"[RailIPC] Sending response: {responseJson.Substring(0, Math.Min(responseJson.Length, 200))}...");
                await WriteMessageAsync(_pipeServer, responseJson);

                _pipeServer.Disconnect();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Log error (TODO: add logging)
            }
            finally
            {
                _pipeServer?.Dispose();
            }
        }
    }

    public string SendCommand(string pipeName, RailCommand command)
    {
        using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

        // Connect with 5 second timeout
        pipeClient.Connect(5000);

        // Send command
        var commandJson = JsonConvert.SerializeObject(command);
        WriteMessageAsync(pipeClient, commandJson).Wait();

        // Read response
        var responseJson = ReadMessageAsync(pipeClient).Result;
        return responseJson;
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _pipeServer?.Dispose();
    }

    private static async Task<string> ReadMessageAsync(PipeStream pipe)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        
        do
        {
            var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length);
            ms.Write(buffer, 0, bytesRead);
        }
        while (!pipe.IsMessageComplete);

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task WriteMessageAsync(PipeStream pipe, string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        await pipe.WriteAsync(buffer, 0, buffer.Length);
        await pipe.FlushAsync();
    }
}



