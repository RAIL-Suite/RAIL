using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RailFactory.Core;

/// <summary>
/// Composite manifest schema v2.0 for multi-module solutions.
/// </summary>
public class CompositeManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "2.0";
    
    [JsonPropertyName("manifest_type")]
    public string ManifestType { get; set; } = "composite";
    
    [JsonPropertyName("solution_name")]
    public string SolutionName { get; set; } = string.Empty;
    
    [JsonPropertyName("modules")]
    public List<ModuleManifest> Modules { get; set; } = new();
    
    [JsonPropertyName("shared_dependencies")]
    public List<SharedDependency> SharedDependencies { get; set; } = new();
    
    [JsonPropertyName("metadata")]
    public ManifestMetadata? Metadata { get; set; }
}

/// <summary>
/// Individual module within a composite manifest.
/// </summary>
public class ModuleManifest
{
    [JsonPropertyName("module_id")]
    public string ModuleId { get; set; } = string.Empty;
    
    [JsonPropertyName("runtime_type")]
    public string RuntimeType { get; set; } = "dotnetbinary";
    
    [JsonPropertyName("entry_point")]
    public string EntryPoint { get; set; } = string.Empty;
    
    /// <summary>
    /// Transport protocol for IPC communication.
    /// Optional - if not specified, inferred from RuntimeType.
    /// Values: "namedpipe", "stdin", "http"
    /// </summary>
    [JsonPropertyName("transport")]
    public string? Transport { get; set; }
    
    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();
    
    [JsonPropertyName("tools")]
    public List<ToolDefinition> Tools { get; set; } = new();
}

/// <summary>
/// Tool/function definition in manifest.
/// </summary>
public class ToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Full qualified class name containing the method.
    /// Used for precise method lookup at runtime.
    /// </summary>
    [JsonPropertyName("class")]
    public string ClassName { get; set; } = string.Empty;
    
    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Shared dependency used by multiple modules.
/// </summary>
public class SharedDependency
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
    
    [JsonPropertyName("used_by")]
    public List<string> UsedBy { get; set; } = new();
}

/// <summary>
/// Optional metadata for manifest.
/// </summary>
public class ManifestMetadata
{
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("generator")]
    public string Generator { get; set; } = "RailStudio";
    
    [JsonPropertyName("generator_version")]
    public string GeneratorVersion { get; set; } = "1.0.0";
}



