using System;

namespace RailFactory.Core;

/// <summary>
/// Extension methods for RuntimeType to support enterprise language detection.
/// </summary>
public static class RuntimeTypeExtensions
{
    /// <summary>
    /// Converts RuntimeType to a standardized language string for the manifest.
    /// This ensures consistent routing throughout the Rail ecosystem.
    /// </summary>
    public static string ToLanguageString(this RuntimeType runtimeType)
    {
        return runtimeType switch
        {
            RuntimeType.DotNetBinary => "csharp",
            RuntimeType.JavaBinary => "java",
            RuntimeType.PythonBinary => "python",
            RuntimeType.NodeBinary => "javascript",
            RuntimeType.GoBinary => "go",
            RuntimeType.CppBinary => "cpp",
            RuntimeType.WebService => "http",
            RuntimeType.Script => "unknown", // Specific script type is usually determined by extension elsewhere
            _ => "unknown"
        };
    }
}



