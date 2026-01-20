// ============================================================================
// Rail ENGINE - UNIVERSAL SDK v2.0 (Enterprise)
// ============================================================================
// Zero-dependency AI-driven application control.
//
// C#:     RailEngine.Ignite(this);  // Uses reflection + manifest
// Python: RailEngine.ignite([ClassA(), ClassB()])  // Registry pattern
// JS/TS:  RailEngine.ignite([new ClassA(), new ClassB()])
// C++:    RailEngine::ignite({{"Method", callback}, ...})
//
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RailSDK
{
    /// <summary>
    /// Main entry point for the Rail SDK.
    /// </summary>
    public static class RailEngine
    {
        private const string PipeName = "RailHost";
        
        // Connection state
        private static NamedPipeClientStream _pipe;
        private static Thread _listenerThread;
        private static CancellationTokenSource _cts;
        private static bool _isIgnited;
        private static string _instanceId;
        
        // Method resolution: className -> (instance, methodCache)
        private static readonly ConcurrentDictionary<string, ClassBinding> _classBindings = new ConcurrentDictionary<string, ClassBinding>();
        
        // Assembly reference for reflection
        private static Assembly _entryAssembly;

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// C# Entry Point - Uses reflection to find classes specified in manifest.
        /// The instance passed is used only to identify the assembly.
        /// </summary>
        public static void Ignite(object appInstance)
        {
            if (appInstance == null)
                throw new ArgumentNullException(nameof(appInstance));

            if (_isIgnited)
                throw new InvalidOperationException("Already ignited. Call Disconnect() first.");

            _entryAssembly = appInstance.GetType().Assembly;
            
            // Auto-register binding for the passed instance
            // This ensures that lookups by class name (from manifest) find this specific instance
            var type = appInstance.GetType();
            var binding = new ClassBinding(appInstance, type);
            _classBindings[type.FullName] = binding;
            _classBindings[type.Name] = binding;
            
            System.Diagnostics.Debug.WriteLine($"[RailSDK] Ignited with instance: {type.FullName}");
            
            _instanceId = Guid.NewGuid().ToString();
            _cts = new CancellationTokenSource();
            _isIgnited = true;

            // Start connection loop in background (Non-blocking)
            _listenerThread = new Thread(ConnectionLoop) { IsBackground = true };
            _listenerThread.Start();
        }

        /// <summary>
        /// Polyglot Entry Point - Register specific class instances.
        /// Use this for Python/JS/TS or when you want explicit control.
        /// </summary>
        public static void Ignite(params object[] instances)
        {
            if (instances == null || instances.Length == 0)
                throw new ArgumentException("At least one instance required", nameof(instances));

            if (_isIgnited)
                throw new InvalidOperationException("Already ignited. Call Disconnect() first.");

            // Register all instances by their class name
            foreach (var instance in instances)
            {
                var type = instance.GetType();
                var binding = new ClassBinding(instance, type);
                _classBindings[type.FullName] = binding;
                _classBindings[type.Name] = binding; // Also register short name
            }

            _entryAssembly = instances[0].GetType().Assembly;
            _instanceId = Guid.NewGuid().ToString();
            _cts = new CancellationTokenSource();
            _isIgnited = true;

            // Start connection loop in background (Non-blocking)
            _listenerThread = new Thread(ConnectionLoop) { IsBackground = true };
            _listenerThread.Start();
        }

        /// <summary>
        /// Disconnect from Rail Host.
        /// </summary>
        public static void Disconnect()
        {
            if (!_isIgnited) return;

            try
            {
                _cts?.Cancel();
                SendMessage(JsonConvert.SerializeObject(new { type = "DISCONNECT" }));
            }
            catch { }

            Cleanup();
            _isIgnited = false;
        }

        /// <summary>
        /// Check if connected.
        /// </summary>
        public static bool IsConnected => _isIgnited && _pipe?.IsConnected == true;

        // ====================================================================
        // CONNECTION & MESSAGE LOOP
        // ====================================================================

        private static void ConnectionLoop()
        {
            System.Diagnostics.Debug.WriteLine("[RailSDK] Connection loop started...");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (_pipe == null || !_pipe.IsConnected)
                    {
                        // Attempt to connect
                        ConnectAndHandshake();
                    }

                    // Process messages while connected
                    if (_pipe?.IsConnected == true)
                    {
                        ProcessMessages();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RailSDK] Connection logic error: {ex.Message}");
                    CleanupPipeOnly();
                }

                // Wait before retrying
                if (!_cts.Token.IsCancellationRequested)
                {
                    Thread.Sleep(2000);
                }
            }
        }

        private static void ConnectAndHandshake()
        {
            try
            {
                CleanupPipeOnly();
                _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                
                System.Diagnostics.Debug.WriteLine("[RailSDK] Attempting to connect to Host...");
                _pipe.Connect(500); // Short timeout for non-blocking feel in loop

                // Send CONNECT message
                var connectMsg = new
                {
                    type = "CONNECT",
                    instanceId = _instanceId,
                    assembly = _entryAssembly?.GetName().Name ?? "Unknown",
                    manifest = new { language = "csharp" }
                };
                SendMessage(JsonConvert.SerializeObject(connectMsg));

                // Wait for ACK
                var ackJson = ReadMessage();
                if (ackJson == null)
                    throw new IOException("Connection closed during handshake");
                
                var ack = JObject.Parse(ackJson);
                if (ack["type"]?.ToString() != "ACK")
                    throw new InvalidOperationException($"Unexpected response: {ackJson}");

                System.Diagnostics.Debug.WriteLine($"[RailSDK] Connected! InstanceId: {_instanceId}");
            }
            catch (TimeoutException) 
            {
                // Expected if Host is not running
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RailSDK] Handshake failed: {ex.Message}");
                CleanupPipeOnly();
            }
        }

        private static void ProcessMessages()
        {
            while (!_cts.Token.IsCancellationRequested && _pipe?.IsConnected == true)
            {
                var msgJson = ReadMessage();
                if (msgJson == null) break; // Pipe broken

                try
                {
                    var msg = JObject.Parse(msgJson);
                    var type = msg["type"]?.ToString();

                    if (type == "EXECUTE")
                    {
                        var requestId = msg["requestId"]?.ToString();
                        var methodName = msg["method"]?.ToString();
                        var className = msg["class"]?.ToString();
                        var args = msg["args"] as JObject;

                        // Execute on ThreadPool to not block the reader loop
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            var result = ExecuteMethod(className, methodName, args);
                            var response = new { type = "RESULT", requestId, result };
                            try { SendMessage(JsonConvert.SerializeObject(response)); } catch { }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RailSDK] Error processing message: {ex.Message}");
                }
            }
        }

        private static void CleanupPipeOnly()
        {
            try
            {
                _pipe?.Dispose();
                _pipe = null;
            }
            catch { }
        }

        private static void Cleanup()
        {
            CleanupPipeOnly();
            _classBindings.Clear();
            _entryAssembly = null;
        }

        // ====================================================================
        // METHOD EXECUTION - POLYGLOT
        // ====================================================================

        private static object ExecuteMethod(string className, string methodName, JObject args)
        {
            if (string.IsNullOrEmpty(methodName))
                return new { status = "error", message = "Missing method name" };

            try
            {
                // Strategy 1: Check registered bindings (polyglot mode)
                if (!string.IsNullOrEmpty(className) && _classBindings.TryGetValue(className, out var binding))
                {
                    return InvokeOnBinding(binding, methodName, args);
                }

                // Strategy 2: Try short class name
                var shortName = className?.Split('.').LastOrDefault();
                if (!string.IsNullOrEmpty(shortName) && _classBindings.TryGetValue(shortName, out binding))
                {
                    return InvokeOnBinding(binding, methodName, args);
                }

                // Strategy 3: Reflection discovery (C# mode)
                if (_entryAssembly != null && !string.IsNullOrEmpty(className))
                {
                    var type = _entryAssembly.GetType(className) 
                            ?? _entryAssembly.GetTypes().FirstOrDefault(t => t.Name == shortName || t.FullName == className);
                    
                    if (type != null)
                    {
                        // Create and cache binding
                        var instance = Activator.CreateInstance(type);
                        binding = new ClassBinding(instance, type);
                        _classBindings[className] = binding;
                        if (!string.IsNullOrEmpty(shortName))
                            _classBindings[shortName] = binding;
                        
                        return InvokeOnBinding(binding, methodName, args);
                    }
                }

                // Strategy 4: Search all registered bindings for the method
                foreach (var kvp in _classBindings)
                {
                    if (kvp.Value.HasMethod(methodName))
                    {
                        return InvokeOnBinding(kvp.Value, methodName, args);
                    }
                }

                // Strategy 5: Deep Reflection Scan (Auto-Discovery)
                // If not found in bindings, scan all types in the entry assembly.
                if (_entryAssembly != null)
                {
                    try
                    {
                        var allTypes = _entryAssembly.GetTypes();
                        foreach (var type in allTypes)
                        {
                            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            if (method != null)
                            {
                                // Found it! Auto-register this type for future speed
                                var instance = Activator.CreateInstance(type);
                                var newBinding = new ClassBinding(instance, type);
                                
                                _classBindings[type.FullName] = newBinding;
                                _classBindings[type.Name] = newBinding;
                                
                                return InvokeOnBinding(newBinding, methodName, args);
                            }
                        }
                    }
                    catch (Exception scanEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RailSDK] Deep scan failed: {scanEx.Message}");
                    }
                }

                return new { status = "error", message = $"Method not found: {className}.{methodName}" };
            }
            catch (Exception ex)
            {
                return new { status = "error", message = ex.InnerException?.Message ?? ex.Message };
            }
        }

        private static object InvokeOnBinding(ClassBinding binding, string methodName, JObject args)
        {
            var method = binding.GetMethod(methodName);
            if (method == null)
                return new { status = "error", message = $"Method not found: {methodName}" };

            var parameters = ConvertParameters(method, args);
            return method.Invoke(binding.Instance, parameters);
        }

        // ====================================================================
        // PARAMETER CONVERSION
        // ====================================================================

        private static object[] ConvertParameters(MethodInfo method, JObject args)
        {
            var paramInfos = method.GetParameters();
            if (paramInfos.Length == 0 || args == null)
                return Array.Empty<object>();

            var parameters = new object[paramInfos.Length];

            for (int i = 0; i < paramInfos.Length; i++)
            {
                var paramInfo = paramInfos[i];
                var paramName = paramInfo.Name;
                var paramType = paramInfo.ParameterType;

                if (args.TryGetValue(paramName, StringComparison.OrdinalIgnoreCase, out var token))
                {
                    parameters[i] = token.ToObject(paramType);
                }
                else if (paramInfo.HasDefaultValue)
                {
                    parameters[i] = paramInfo.DefaultValue;
                }
                else
                {
                    parameters[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                }
            }

            return parameters;
        }

        // ====================================================================
        // PIPE I/O (Length-prefixed protocol)
        // ====================================================================

        private static void SendMessage(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var lenBytes = BitConverter.GetBytes(bytes.Length);
            
            lock (_pipe)
            {
                _pipe.Write(lenBytes, 0, 4);
                _pipe.Write(bytes, 0, bytes.Length);
                _pipe.Flush();
            }
        }

        private static string ReadMessage()
        {
            try
            {
                var lenBuf = new byte[4];
                var read = _pipe.Read(lenBuf, 0, 4);
                if (read < 4) return null;

                var len = BitConverter.ToInt32(lenBuf, 0);
                if (len <= 0 || len > 1048576) return null; // 1MB max

                var buf = new byte[len];
                var totalRead = 0;
                while (totalRead < len)
                {
                    var n = _pipe.Read(buf, totalRead, len - totalRead);
                    if (n == 0) return null;
                    totalRead += n;
                }

                return Encoding.UTF8.GetString(buf);
            }
            catch
            {
                return null;
            }
        }
    }

    // ========================================================================
    // CLASS BINDING - Caches instance and method lookups
    // ========================================================================

    internal sealed class ClassBinding
    {
        public object Instance { get; }
        private readonly Dictionary<string, MethodInfo> _methods;

        public ClassBinding(object instance, Type type)
        {
            Instance = instance;
            _methods = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!method.IsSpecialName)
                    _methods[method.Name] = method;
            }
        }

        public bool HasMethod(string name) => _methods.ContainsKey(name);
        public MethodInfo GetMethod(string name) => _methods.TryGetValue(name, out var m) ? m : null;
    }
}



