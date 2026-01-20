using System.Text.Json;
using WpfRagApp.Services.ApiOrchestration.Models;

namespace WpfRagApp.Services.ApiOrchestration.Ingestion;

/// <summary>
/// Interface for the API ingestion orchestrator.
/// Coordinates parsing, embedding, and storage of API specs.
/// </summary>
public interface IIngestionService
{
    /// <summary>
    /// Import an API from an OpenAPI/Swagger URL.
    /// </summary>
    Task<IngestionResult> ImportFromUrlAsync(string url, string providerId, IProgress<IngestionProgress>? progress = null);
    
    /// <summary>
    /// Import an API from an OpenAPI/Swagger JSON string.
    /// </summary>
    Task<IngestionResult> ImportFromJsonAsync(string json, string providerId, IProgress<IngestionProgress>? progress = null);
    
    /// <summary>
    /// Delete all imported data for a provider.
    /// </summary>
    Task DeleteProviderAsync(string providerId);
    
    /// <summary>
    /// Get list of all imported providers.
    /// </summary>
    Task<List<string>> GetImportedProvidersAsync();
}

/// <summary>
/// Result of an API ingestion operation.
/// </summary>
public class IngestionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ProviderId { get; set; }
    public int SkillCount { get; set; }
    public ApiManifest? Manifest { get; set; }
    
    public static IngestionResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Progress update during ingestion.
/// </summary>
public class IngestionProgress
{
    public string Stage { get; set; } = "";
    public int Current { get; set; }
    public int Total { get; set; }
    public string? Message { get; set; }
    
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}





