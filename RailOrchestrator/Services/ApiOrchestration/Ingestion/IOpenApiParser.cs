using WpfRagApp.Services.ApiOrchestration.Models;

namespace WpfRagApp.Services.ApiOrchestration.Ingestion;

/// <summary>
/// Interface for parsing OpenAPI/Swagger specifications.
/// Converts raw spec into normalized UniversalApiSkill objects.
/// </summary>
public interface IOpenApiParser
{
    /// <summary>
    /// Parse an OpenAPI specification from JSON string.
    /// </summary>
    /// <param name="jsonSpec">Raw OpenAPI JSON content</param>
    /// <param name="providerId">Provider identifier for the skills</param>
    /// <returns>Parsed result with skills and manifest info</returns>
    Task<OpenApiParseResult> ParseAsync(string jsonSpec, string providerId);
    
    /// <summary>
    /// Parse an OpenAPI specification from URL.
    /// </summary>
    Task<OpenApiParseResult> ParseFromUrlAsync(string url, string providerId);
    
    /// <summary>
    /// Detect the OpenAPI version from spec.
    /// </summary>
    string DetectVersion(string jsonSpec);
}

/// <summary>
/// Result of parsing an OpenAPI specification.
/// </summary>
public class OpenApiParseResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    
    /// <summary>
    /// Provider information extracted from spec.
    /// </summary>
    public ProviderInfo? Provider { get; set; }
    
    /// <summary>
    /// Authentication configuration from security schemes.
    /// </summary>
    public AuthConfig? Auth { get; set; }
    
    /// <summary>
    /// List of parsed skills (one per endpoint).
    /// </summary>
    public List<UniversalApiSkill> Skills { get; set; } = new();
    
    /// <summary>
    /// OpenAPI version detected.
    /// </summary>
    public string? Version { get; set; }
    
    /// <summary>
    /// Source URL if parsed from URL.
    /// </summary>
    public string? SourceUrl { get; set; }
    
    public static OpenApiParseResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}





