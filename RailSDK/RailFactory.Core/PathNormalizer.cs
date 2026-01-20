using System;
using System.IO;
using dnlib.DotNet;

namespace RailFactory.Core;

/// <summary>
/// Interface for normalizing executable paths to analyzable assembly paths.
/// Polyglot-ready: implement for each supported runtime.
/// </summary>
public interface IPathNormalizer
{
    /// <summary>
    /// Normalizes an executable path to get the analyzable assembly/module.
    /// For .NET: converts native apphost exe to managed dll.
    /// For Python: returns the main .py script.
    /// </summary>
    string GetAnalyzablePath(string executablePath);
    
    /// <summary>
    /// Validates if the path can be analyzed for dependencies.
    /// </summary>
    bool IsValidForAnalysis(string path);
    
    /// <summary>
    /// Gets the runtime type this normalizer handles.
    /// </summary>
    RuntimeType SupportedRuntime { get; }
}

/// <summary>
/// Path normalizer for .NET assemblies.
/// Handles native apphost wrappers by finding the corresponding managed DLL.
/// </summary>
public class DotNetPathNormalizer : IPathNormalizer
{
    public RuntimeType SupportedRuntime => RuntimeType.DotNetBinary;
    
    public string GetAnalyzablePath(string executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
            return executablePath;
        
        // Use existing logic from RuntimeRegistry
        return RuntimeRegistry.GetManagedAssemblyPath(executablePath);
    }
    
    public bool IsValidForAnalysis(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;
        
        try
        {
            // Try to load as .NET assembly using dnlib
            using var module = ModuleDefMD.Load(path);
            return module?.Assembly != null;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Path normalizer for Python scripts.
/// Future implementation for polyglot support.
/// </summary>
public class PythonPathNormalizer : IPathNormalizer
{
    public RuntimeType SupportedRuntime => RuntimeType.PythonBinary;
    
    public string GetAnalyzablePath(string executablePath)
    {
        // Python scripts are directly analyzable
        return executablePath;
    }
    
    public bool IsValidForAnalysis(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;
        
        // Check if it's a valid Python file
        return path.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Factory for getting the appropriate path normalizer based on runtime type.
/// </summary>
public static class PathNormalizerFactory
{
    private static readonly DotNetPathNormalizer _dotNetNormalizer = new();
    private static readonly PythonPathNormalizer _pythonNormalizer = new();
    
    public static IPathNormalizer GetNormalizer(RuntimeType runtimeType)
    {
        return runtimeType switch
        {
            RuntimeType.DotNetBinary => _dotNetNormalizer,
            RuntimeType.PythonBinary => _pythonNormalizer,
            _ => _dotNetNormalizer // Default to .NET
        };
    }
    
    /// <summary>
    /// Gets normalizer by detecting the file type.
    /// </summary>
    public static IPathNormalizer GetNormalizerForFile(string filePath)
    {
        var runtimeType = RuntimeRegistry.DetectRuntime(filePath);
        return GetNormalizer(runtimeType);
    }
}



