using System;
using System.IO;
using System.Reflection;

namespace RailFactory.Core;

/// <summary>
/// Helper class to extract embedded Python resources from the DLL
/// </summary>
internal static class EmbeddedResourceExtractor
{
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "RailSDK", "runtime");
    private static bool _extracted = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Extracts all embedded Python runtime files to a temporary directory
    /// Returns the path to the extracted cli/runtime.py
    /// </summary>
    public static string ExtractRuntime()
    {
        lock (_lock)
        {
            if (_extracted)
            {
                return Path.Combine(TempDir, "cli", "runtime.py");
            }

            // Create temp directory structure
            Directory.CreateDirectory(TempDir);
            Directory.CreateDirectory(Path.Combine(TempDir, "cli"));
            Directory.CreateDirectory(Path.Combine(TempDir, "core"));
            Directory.CreateDirectory(Path.Combine(TempDir, "parsers"));
            Directory.CreateDirectory(Path.Combine(TempDir, "runners"));

            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();

            foreach (var resourceName in resourceNames)
            {
                if (!resourceName.StartsWith("RailSDK."))
                    continue;

                // Extract resource
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    continue;

                // Determine output path
                // Resource name format: RailSDK.cli.runtime.py → cli/runtime.py
                var relativePath = resourceName.Substring("RailSDK.".Length);
                var parts = relativePath.Split('.');
                
                // Last two parts are filename.extension
                var fileName = parts[^2] + "." + parts[^1];
                var folder = string.Join(Path.DirectorySeparatorChar, parts[..^2]);
                
                var outputPath = Path.Combine(TempDir, folder, fileName);
                
                // Write to file
                using var fileStream = File.Create(outputPath);
                stream.CopyTo(fileStream);
            }

            _extracted = true;
            return Path.Combine(TempDir, "cli", "runtime.py");
        }
    }

    /// <summary>
    /// Cleans up extracted files (optional, call on app shutdown)
    /// </summary>
    public static void Cleanup()
    {
        try
        {
            if (Directory.Exists(TempDir))
            {
                Directory.Delete(TempDir, recursive: true);
            }
            _extracted = false;
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}



