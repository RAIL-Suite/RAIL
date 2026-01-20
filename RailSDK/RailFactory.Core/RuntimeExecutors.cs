using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace RailFactory.Core;

/// <summary>
/// Enterprise-grade executor with automatic UI thread marshalling.
/// Uses SynchronizationContext for universal framework support (WPF, WinForms, Console).
/// Features:
/// - Assembly-wide method discovery (searches all public classes)
/// - Method cache for O(1) lookup after first scan
/// - Support for class-qualified method names (Class.Method)
/// </summary>
public class DotNetRuntimeExecutor : IRuntimeExecutor
{
    private object? _appInstance;
    private Assembly? _assembly;
    private SynchronizationContext? _syncContext;
    
    // Cache: methodName -> (Type, MethodInfo, Instance)
    // Instance is created once per type for non-static methods
    private Dictionary<string, (Type Type, MethodInfo Method, object? Instance)>? _methodCache;
    private readonly object _cacheLock = new();
    
    public void Initialize(object appInstance)
    {
        _appInstance = appInstance ?? throw new ArgumentNullException(nameof(appInstance));
        _assembly = appInstance.GetType().Assembly;
        
        // Capture SynchronizationContext from initialization thread
        // (Should be called from UI thread in WPF/WinForms apps)
        _syncContext = SynchronizationContext.Current;
        
        // Build method cache lazily on first Execute
        _methodCache = null;
    }
    
    public object Execute(RailCommand command)
    {
        System.Diagnostics.Debug.WriteLine($"[RailEngine] Execute called: {command.MethodName}");
        
        if (_appInstance == null || _assembly == null)
        {
            System.Diagnostics.Debug.WriteLine("[RailEngine] ERROR: Executor not initialized");
            throw new InvalidOperationException("Executor not initialized. Call Initialize() first.");
        }
        
        // Ensure cache is built (thread-safe, one-time operation)
        EnsureMethodCacheBuilt();
        System.Diagnostics.Debug.WriteLine($"[RailEngine] Cache built: {_methodCache?.Count ?? 0} methods");
        
        // Find method in cache (qualified name first, then fallback)
        var (declaringType, method, instance) = FindMethod(command.QualifiedMethodName, command.MethodName);
        System.Diagnostics.Debug.WriteLine($"[RailEngine] Found method: {declaringType.Name}.{method.Name}, Instance: {instance != null}");
        
        // Convert JSON args to typed parameters
        var parameterInfos = method.GetParameters();
        var parameters = ConvertJsonToParameters(command.Args, parameterInfos);
        System.Diagnostics.Debug.WriteLine($"[RailEngine] Parameters converted: {parameters.Length} args");
        
        // UI app with SynchronizationContext? Marshal to UI thread
        if (_syncContext != null)
        {
            System.Diagnostics.Debug.WriteLine("[RailEngine] Marshalling to UI thread...");
            object? result = null;
            Exception? exception = null;
            
            _syncContext.Send(_ =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[RailEngine] Invoking method on UI thread...");
                    result = InvokeMethod(method, instance, parameters);
                    System.Diagnostics.Debug.WriteLine($"[RailEngine] Method returned: {result?.GetType().Name ?? "null"}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RailEngine] Method threw exception: {ex.Message}");
                    exception = ex;
                }
            }, null);
            
            if (exception != null)
                throw exception;
                
            return result ?? new object();
        }
        
        // Console app or no sync context - invoke directly
        System.Diagnostics.Debug.WriteLine("[RailEngine] Invoking directly (no SyncContext)...");
        return InvokeMethod(method, instance, parameters);
    }
    
    /// <summary>
    /// Builds method cache from all public classes in the assembly.
    /// Called once on first Execute, results cached for O(1) subsequent lookups.
    /// </summary>
    private void EnsureMethodCacheBuilt()
    {
        if (_methodCache != null) return;
        
        lock (_cacheLock)
        {
            if (_methodCache != null) return; // Double-check after lock
            
            var cache = new Dictionary<string, (Type, MethodInfo, object?)>(StringComparer.OrdinalIgnoreCase);
            var instances = new Dictionary<Type, object>(); // Reuse instances per type
            
            foreach (var type in _assembly!.GetTypes())
            {
                // Skip non-public, abstract, interfaces, compiler-generated
                if (!type.IsPublic && !type.IsNestedPublic) continue;
                if (type.IsAbstract || type.IsInterface) continue;
                if (type.Name.StartsWith("<")) continue; // Compiler-generated
                
                // Skip UI types that require STA thread (WPF/WinForms)
                if (IsUIType(type)) continue;
                
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                
                foreach (var method in methods)
                {
                    // Skip inherited from Object, property accessors
                    if (method.DeclaringType == typeof(object)) continue;
                    if (method.IsSpecialName) continue; // get_X, set_X, etc.
                    
                    object? instance = null;
                    if (!method.IsStatic)
                    {
                        // Get or create instance for this type
                        if (!instances.TryGetValue(type, out instance))
                        {
                            try
                            {
                                instance = Activator.CreateInstance(type);
                                instances[type] = instance!;
                            }
                            catch
                            {
                                // Skip types without parameterless constructor
                                continue;
                            }
                        }
                    }
                    
                    // Register by simple name (first wins if duplicates)
                    if (!cache.ContainsKey(method.Name))
                    {
                        cache[method.Name] = (type, method, instance);
                    }
                    
                    // Also register by fully qualified name: ClassName.MethodName
                    var qualifiedName = $"{type.Name}.{method.Name}";
                    if (!cache.ContainsKey(qualifiedName))
                    {
                        cache[qualifiedName] = (type, method, instance);
                    }
                    
                    // And full namespace qualified: Namespace.ClassName.MethodName
                    var fullQualifiedName = $"{type.FullName}.{method.Name}";
                    if (!cache.ContainsKey(fullQualifiedName))
                    {
                        cache[fullQualifiedName] = (type, method, instance);
                    }
                }
            }
            
            _methodCache = cache;
        }
    }
    
    /// <summary>
    /// Finds method by name. Tries qualified name first (Class.Method), then fallback to simple name.
    /// Supports: MethodName, ClassName.MethodName, Full.Namespace.ClassName.MethodName
    /// </summary>
    /// <param name="qualifiedName">Fully qualified name (Namespace.Class.Method) - tried first</param>
    /// <param name="fallbackName">Simple method name - tried if qualified not found</param>
    private (Type Type, MethodInfo Method, object? Instance) FindMethod(string qualifiedName, string fallbackName)
    {
        // 1. Try fully qualified name first (when class is specified in manifest)
        if (_methodCache!.TryGetValue(qualifiedName, out var entry))
        {
            System.Diagnostics.Debug.WriteLine($"[RailEngine] Method found by qualified name: {qualifiedName}");
            return entry;
        }
        
        // 2. Fallback to simple method name (backward compatibility)
        if (qualifiedName != fallbackName && _methodCache!.TryGetValue(fallbackName, out entry))
        {
            System.Diagnostics.Debug.WriteLine($"[RailEngine] Method found by fallback name: {fallbackName}");
            return entry;
        }
        
        throw new MissingMethodException(
            $"Method '{qualifiedName}' not found in assembly '{_assembly!.GetName().Name}'. " +
            $"Searched {_methodCache.Count} methods across all public classes.");
    }
    
    /// <summary>
    /// Checks if a type is a UI type that requires STA thread (WPF, WinForms).
    /// These types cannot be instantiated on a background thread.
    /// </summary>
    private static bool IsUIType(Type type)
    {
        // Check by base type names (avoids requiring WPF references)
        var baseType = type.BaseType;
        while (baseType != null)
        {
            var baseName = baseType.FullName ?? baseType.Name;
            
            // WPF types
            if (baseName.StartsWith("System.Windows.Window")) return true;
            if (baseName.StartsWith("System.Windows.Controls.UserControl")) return true;
            if (baseName.StartsWith("System.Windows.Controls.Page")) return true;
            if (baseName.StartsWith("System.Windows.Controls.Control")) return true;
            if (baseName.StartsWith("System.Windows.Application")) return true;
            if (baseName.StartsWith("System.Windows.FrameworkElement")) return true;
            
            // WinForms types
            if (baseName.StartsWith("System.Windows.Forms.Form")) return true;
            if (baseName.StartsWith("System.Windows.Forms.UserControl")) return true;
            if (baseName.StartsWith("System.Windows.Forms.Control")) return true;
            
            baseType = baseType.BaseType;
        }
        
        // Also check by type name patterns (ViewModels often have DI issues too)
        var typeName = type.Name;
        if (typeName.EndsWith("Window")) return true;
        if (typeName.EndsWith("View") && !typeName.Contains("Model")) return true;
        
        return false;
    }
    
    /// <summary>
    /// Invokes method with proper exception handling.
    /// </summary>
    private object InvokeMethod(MethodInfo method, object? instance, object?[] parameters)
    {
        try
        {
            return method.Invoke(instance, parameters) ?? new object();
        }
        catch (TargetInvocationException ex)
        {
            // Unwrap inner exception for clarity
            throw new InvalidOperationException(
                $"Method '{method.Name}' threw an exception", 
                ex.InnerException ?? ex);
        }
    }

    
    private object?[] ConvertJsonToParameters(JObject argsJson, ParameterInfo[] paramInfos)
    {
        var parameters = new object?[paramInfos.Length];
        
        for (int i = 0; i < paramInfos.Length; i++)
        {
            var paramInfo = paramInfos[i];
            var paramName = paramInfo.Name ?? $"arg{i}";
            
            if (!argsJson.TryGetValue(paramName, out var token))
            {
                if (paramInfo.IsOptional)
                {
                    parameters[i] = paramInfo.DefaultValue;
                    continue;
                }
                
                throw new ArgumentException(
                    $"Missing required parameter: {paramName}");
            }
            
            // Deserialize to correct type
            parameters[i] = token.ToObject(paramInfo.ParameterType);
        }
        
        return parameters;
    }
}



