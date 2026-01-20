using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RailFactory.Core;

/// <summary>
/// Scans a folder to detect all valid executables for solution-wide manifest generation.
/// </summary>
public class SolutionScanner
{
    public SolutionScanResult ScanFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        
        var result = new SolutionScanResult
        {
            ScanPath = folderPath,
            Executables = new List<ExecutableInfo>(),
            AllFiles = new List<string>()
        };
        
        // Scan for .NET executables
        var exeFiles = Directory.GetFiles(folderPath, "*.exe", SearchOption.AllDirectories);
        result.AllFiles.AddRange(exeFiles);
        
        foreach (var exePath in exeFiles)
        {
            try
            {
                var runtimeType = RuntimeRegistry.DetectRuntime(exePath);
                
                if (runtimeType == RuntimeType.DotNetBinary)
                {
                    result.Executables.Add(new ExecutableInfo
                    {
                        Path = exePath,
                        Name = Path.GetFileNameWithoutExtension(exePath),
                        RuntimeType = runtimeType,
                        Size = new FileInfo(exePath).Length
                    });
                }
            }
            catch
            {
                // Skip invalid exe files
                continue;
            }
        }
        
        return result;
    }
}

/// <summary>
/// Result of a solution scan operation.
/// </summary>
public class SolutionScanResult
{
    public string ScanPath { get; set; } = string.Empty;
    public List<ExecutableInfo> Executables { get; set; } = new();
    public List<string> AllFiles { get; set; } = new();
    
    public bool HasContent => Executables.Count > 0;
}

/// <summary>
/// Information about a detected executable.
/// </summary>
public class ExecutableInfo
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public RuntimeType RuntimeType { get; set; }
    public long Size { get; set; }
}



