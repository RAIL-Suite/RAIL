using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;

namespace RailFactory.Core;

/// <summary>
/// Analyzes assembly dependencies to build a complete dependency graph.
/// Uses Assembly.GetReferencedAssemblies() for dependency discovery.
/// </summary>
public class DependencyAnalyzer
{
    private readonly HashSet<string> _processedAssemblies = new();
    private readonly List<DependencyInfo> _allDependencies = new();
    
    public DependencyGraph AnalyzeDependencies(List<string> exePaths)
    {
        _processedAssemblies.Clear();
        _allDependencies.Clear();
        
        var graph = new DependencyGraph
        {
            Modules = new Dictionary<string, ModuleNode>(),
            Dependencies = new List<DependencyInfo>()
        };
        
        var normalizer = new DotNetPathNormalizer();
        
        foreach (var exePath in exePaths)
        {
            var moduleName = Path.GetFileNameWithoutExtension(exePath);
            var moduleNode = new ModuleNode
            {
                ModuleName = moduleName,
                EntryPoint = exePath,
                DirectDependencies = new List<string>()
            };
            
            // CRITICAL: Normalize path before analysis
            // Native apphost exe -> managed dll
            var analyzablePath = normalizer.GetAnalyzablePath(exePath);
            var basePath = Path.GetDirectoryName(analyzablePath)!;
            
            // Validate before attempting analysis
            if (normalizer.IsValidForAnalysis(analyzablePath))
            {
                AnalyzeAssemblyRecursive(analyzablePath, moduleName, basePath);
            }
            
            graph.Modules[moduleName] = moduleNode;
        }
        
        // Build shared dependencies list
        graph.Dependencies = _allDependencies
            .GroupBy(d => d.Name)
            .Select(g => {
                var first = g.First();
                first.UsedBy = g.SelectMany(d => d.UsedBy).Distinct().ToList();
                first.UsedByCount = first.UsedBy.Count;
                return first;
            })
            .ToList();
        
        return graph;
    }
    
    private void AnalyzeAssemblyRecursive(string assemblyPath, string parentModule, string basePath)
    {
        if (_processedAssemblies.Contains(assemblyPath))
            return;
        
        _processedAssemblies.Add(assemblyPath);
        
        try
        {
            // Use dnlib for zero-dependency assembly analysis
            // Works with ALL .NET versions without requiring core assemblies
            using var module = ModuleDefMD.Load(assemblyPath);
            var referencedAssemblies = module.GetAssemblyRefs();
            
            foreach (var refAsm in referencedAssemblies)
            {
                // Try to find the assembly in the base path
                var dllPath = Path.Combine(basePath, refAsm.Name + ".dll");
                
                if (File.Exists(dllPath))
                {
                    var depInfo = new DependencyInfo
                    {
                        Name = refAsm.Name.String,
                        Path = dllPath,
                        Version = refAsm.Version?.ToString() ?? "Unknown",
                        UsedBy = new List<string> { parentModule }
                    };
                    
                    _allDependencies.Add(depInfo);
                    
                    // Recursive analysis for transitive dependencies
                    AnalyzeAssemblyRecursive(dllPath, parentModule, basePath);
                }
            }
        }
        catch (Exception ex)
        {
            // Store error for debugging - silent failures are enterprise anti-pattern
            LastError = $"Failed to analyze {assemblyPath}: {ex.Message}";
            LastException = ex;
        }
    }
    
    /// <summary>
    /// Last error message for debugging. Check after AnalyzeDependencies returns 0 deps.
    /// </summary>
    public string? LastError { get; private set; }
    
    /// <summary>
    /// Last exception for detailed debugging.
    /// </summary>
    public Exception? LastException { get; private set; }
}

/// <summary>
/// Complete dependency graph for a solution.
/// </summary>
public class DependencyGraph
{
    public Dictionary<string, ModuleNode> Modules { get; set; } = new();
    public List<DependencyInfo> Dependencies { get; set; } = new();
    
    public List<DependencyInfo> GetDependenciesFor(string moduleName)
    {
        return Dependencies.Where(d => d.UsedBy.Contains(moduleName)).ToList();
    }
    
    public List<DependencyInfo> GetSharedDependencies()
    {
        return Dependencies.Where(d => d.UsedByCount > 1).ToList();
    }
}

/// <summary>
/// Node representing a module in the dependency graph.
/// </summary>
public class ModuleNode
{
    public string ModuleName { get; set; } = string.Empty;
    public string EntryPoint { get; set; } = string.Empty;
    public List<string> DirectDependencies { get; set; } = new();
}

/// <summary>
/// Information about a dependency (DLL).
/// </summary>
public class DependencyInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<string> UsedBy { get; set; } = new();
    public int UsedByCount { get; set; }
    public AssemblyClassification Classification { get; set; }
}



