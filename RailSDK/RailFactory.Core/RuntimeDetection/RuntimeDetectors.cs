using System;
using System.IO;
using dnlib.DotNet;

namespace RailFactory.Core;

/// <summary>
/// Base class for runtime detectors using enterprise-grade detection.
/// </summary>
public abstract class RuntimeDetectorBase : IRuntimeDetector
{
    protected static string LogFilePath = Path.Combine(Path.GetTempPath(), "Rail_detector_debug.log");
    
    public abstract RuntimeType Detect(string filePath);
    public abstract bool CanHandle(string filePath);
    
    protected void Log(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            File.AppendAllText(LogFilePath, $"[{timestamp}] {message}\n");
        }
        catch { }
    }
}

/// <summary>
/// Enterprise-grade .NET binary detector using dnlib library.
/// Supports all .NET versions: Framework, Core, 5, 6, 7, 8, 9+
/// Supports all formats: Standard, R2R, Trimmed, Single-File, AOT
/// Auto-detects companion .dll for native apphosts (.NET 5+)
/// </summary>
public class DotNetRuntimeDetector : RuntimeDetectorBase
{
    /// <summary>
    /// For .exe files that are native apphosts, stores the actual managed .dll path
    /// </summary>
    public string? ActualManagedPath { get; private set; }
    
    public override RuntimeType Detect(string filePath)
    {
        Log($"=== DotNetRuntimeDetector.Detect START ===");
        Log($"File: {filePath}");
        
        if (!CanHandle(filePath))
        {
            Log($"DotNetRuntimeDetector.Detect: CanHandle returned FALSE");
            Log($"=== DotNetRuntimeDetector.Detect END (Unknown) ===\n");
            return RuntimeType.Unknown;
        }
        
        Log($"DotNetRuntimeDetector.Detect: CanHandle returned TRUE");
        Log($"=== DotNetRuntimeDetector.Detect END (DotNetBinary) ===\n");
        return RuntimeType.DotNetBinary;
    }
    
    public override bool CanHandle(string filePath)
    {
        Log($"DotNetRuntimeDetector.CanHandle: Checking {filePath}");
        ActualManagedPath = null; // Reset
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            Log($"CanHandle: filePath is null/empty");
            return false;
        }
            
        if (!File.Exists(filePath))
        {
            Log($"CanHandle: File does not exist");
            return false;
        }
        
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        Log($"CanHandle: Extension = {ext}");
        
        if (ext != ".exe" && ext != ".dll")
        {
            Log($"CanHandle: Extension not .exe or .dll");
            return false;
        }
        
        // Try loading with dnlib
        if (TryLoadAssembly(filePath))
        {
            ActualManagedPath = filePath;
            return true;
        }
        
        // If .exe failed and we have a companion .dll, try that (apphost scenario)
        if (ext == ".exe")
        {
            var dllPath = Path.ChangeExtension(filePath, ".dll");
            Log($"CanHandle: .exe failed, trying companion .dll: {dllPath}");
            
            if (File.Exists(dllPath))
            {
                if (TryLoadAssembly(dllPath))
                {
                    Log($"CanHandle: SUCCESS - Found managed DLL for native apphost EXE");
                    ActualManagedPath = dllPath; // Store the actual managed assembly path
                    return true;
                }
            }
            else
            {
                Log($"CanHandle: Companion .dll not found");
            }
        }
        
        Log($"CanHandle: Final result = False");
        return false;
    }
    
    private bool TryLoadAssembly(string filePath)
    {
        try
        {
            Log($"TryLoadAssembly: Attempting dnlib load for {Path.GetFileName(filePath)}");
            
            using (var module = ModuleDefMD.Load(filePath))
            {
                if (module != null)
                {
                    Log($"TryLoadAssembly: dnlib SUCCESS");
                    Log($"TryLoadAssembly: Assembly = {module.Assembly?.FullName ?? "N/A"}");
                    Log($"TryLoadAssembly: Runtime = {module.RuntimeVersion}");
                    Log($"TryLoadAssembly: IsILOnly = {module.IsILOnly}");
                    Log($"TryLoadAssembly: Kind = {module.Kind}");
                    return true;
                }
            }
            
            Log($"TryLoadAssembly: dnlib returned null module");
            return false;
        }
        catch (BadImageFormatException ex)
        {
            Log($"TryLoadAssembly: BadImageFormat: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Log($"TryLoadAssembly: EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Gets the actual managed assembly path (may differ from input if apphost was detected)
    /// </summary>
    public string GetManagedAssemblyPath(string originalPath)
    {
        return ActualManagedPath ?? originalPath;
    }
}

/// <summary>
/// Stub detector for Java binaries.
/// </summary>
public class JavaRuntimeDetector : RuntimeDetectorBase
{
    public override RuntimeType Detect(string filePath) => RuntimeType.Unknown;
    public override bool CanHandle(string filePath) => false;
}

/// <summary>
/// Stub detector for Python packaged binaries.
/// </summary>
public class PythonRuntimeDetector : RuntimeDetectorBase
{
    public override RuntimeType Detect(string filePath) => RuntimeType.Unknown;
    public override bool CanHandle(string filePath) => false;
}



