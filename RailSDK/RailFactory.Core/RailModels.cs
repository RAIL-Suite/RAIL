using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RailFactory.Core;

/// <summary>
/// Represents a method extracted from a binary that can be invoked by the LLM.
/// </summary>
public class RailMethod
{
    public string ClassName { get; set; }
    public string MethodName { get; set; }
    public string Description { get; set; }
    public Type ReturnType { get; set; }
    public List<RailParameter> Parameters { get; set; } = new();
    
    /// <summary>
    /// Optional: XML documentation comments from source code
    /// </summary>
    public string? XmlDocumentation { get; set; }
}

/// <summary>
/// Represents a parameter of a RailMethod
/// </summary>
public class RailParameter
{
    public string Name { get; set; }
    public Type ParameterType { get; set; }
    public bool IsOptional { get; set; }
    public object? DefaultValue { get; set; }
    
    /// <summary>
    /// JSON Schema for this parameter (Gemini-compatible).
    /// Contains type, properties (for complex types), required fields, etc.
    /// </summary>
    public Dictionary<string, object>? ParameterSchema { get; set; }
}

/// <summary>
/// Enterprise polyglot command protocol for cross-language IPC.
/// Supports multiple naming conventions for maximum compatibility:
/// - Python/Ruby style: function, args
/// - JavaScript/Java style: method, arguments
/// - .NET style: MethodName, Parameters
/// - JSON-RPC 2.0 style: method, params
/// 
/// Version-aware protocol enables future evolution without breaking changes.
/// </summary>
public class RailCommand
{
    /// <summary>
    /// Name of the method to invoke.
    /// Accepts: "method", "function", "methodName", "MethodName"
    /// </summary>
    [JsonProperty("method")]
    public string? Method { get; set; }
    
    [JsonProperty("function")]
    public string? Function { get; set; }
    
    [JsonProperty("methodName")]
    public string? MethodNameCamel { get; set; }
    
    [JsonProperty("MethodName")]
    public string? MethodNamePascal { get; set; }
    
    /// <summary>
    /// Gets the actual method name from any of the variant properties.
    /// </summary>
    [JsonIgnore]
    public string MethodName => 
        Method ?? Function ?? MethodNameCamel ?? MethodNamePascal 
        ?? throw new ArgumentException("Command must specify method name (method/function/methodName/MethodName)");
    
    /// <summary>
    /// Class name containing the method (from manifest).
    /// When specified, enables class-aware method resolution to avoid name collisions.
    /// </summary>
    [JsonProperty("class")]
    public string? Class { get; set; }
    
    /// <summary>
    /// Gets fully qualified method name for cache lookup.
    /// Returns "Namespace.Class.Method" if class specified, otherwise just "Method".
    /// </summary>
    [JsonIgnore]
    public string QualifiedMethodName => 
        string.IsNullOrEmpty(Class) ? MethodName : $"{Class}.{MethodName}";
    
    /// <summary>
    /// Arguments as JSON object.
    /// Accepts: "params", "arguments", "args", "Args", "Parameters"
    /// </summary>
    [JsonProperty("params")]
    public JObject? Params { get; set; }
    
    [JsonProperty("arguments")]
    public JObject? Arguments { get; set; }
    
    [JsonProperty("args")]
    public JObject? ArgsLower { get; set; }
    
    [JsonProperty("Args")]
    public JObject? ArgsPascal { get; set; }
    
    [JsonProperty("Parameters")]
    public JObject? Parameters { get; set; }
    
    /// <summary>
    /// Gets the actual arguments from any of the variant properties.
    /// </summary>
    [JsonIgnore]
    public JObject Args => 
        Params ?? Arguments ?? ArgsLower ?? ArgsPascal ?? Parameters 
        ?? new JObject(); // Empty object for parameterless methods
    
    /// <summary>
    /// Optional: Target class name if multiple classes have the same method
    /// </summary>
    [JsonProperty("className")]
    public string? ClassName { get; set; }
    
    /// <summary>
    /// Protocol version for future evolution.
    /// Default is "1.0" for initial release.
    /// Future breaking changes will increment major version (e.g., "2.0")
    /// </summary>
    [JsonProperty("version")]
    public string Version { get; set; } = "1.0";
    
    /// <summary>
    /// Optional metadata for tracing, authentication, etc.
    /// Reserved for future use.
    /// </summary>
    [JsonProperty("metadata")]
    public JObject? Metadata { get; set; }
    
    /// <summary>
    /// Optional request ID for correlation (JSON-RPC 2.0 compatible)
    /// </summary>
    [JsonProperty("id")]
    public string? Id { get; set; }
}

/// <summary>
/// Standard response format for command execution.
/// Compatible with JSON-RPC 2.0 success responses.
/// </summary>
public class RailResponse
{
    /// <summary>
    /// "success" or "error"
    /// </summary>
    [JsonProperty("status")]
    public string Status { get; set; }
    
    /// <summary>
    /// Result value (null if error)
    /// </summary>
    [JsonProperty("result")]
    public object? Result { get; set; }
    
    /// <summary>
    /// Error message (null if success)
    /// </summary>
    [JsonProperty("error")]
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Request ID for correlation (matches command.Id)
    /// </summary>
    [JsonProperty("id")]
    public string? Id { get; set; }
    
    /// <summary>
    /// Protocol version
    /// </summary>
    [JsonProperty("version")]
    public string Version { get; set; } = "1.0";
    
    public static RailResponse Success(object? result, string? id = null)
    {
        return new RailResponse
        {
            Status = "success",
            Result = result,
            Id = id
        };
    }
    
    public static RailResponse Error(string errorMessage, string? id = null)
    {
        return new RailResponse
        {
            Status = "error",
            ErrorMessage = errorMessage,
            Id = id
        };
    }
}



