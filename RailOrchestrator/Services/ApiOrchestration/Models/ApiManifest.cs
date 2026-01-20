namespace WpfRagApp.Services.ApiOrchestration.Models;

/// <summary>
/// API provider manifest - describes an API integration.
/// Stored as manifest.json in the provider's folder.
/// </summary>
public class ApiManifest
{
    public string ManifestVersion { get; set; } = "2.0";
    public string Type { get; set; } = "api";
    
    /// <summary>
    /// Provider identification and display info
    /// </summary>
    public required ProviderInfo Provider { get; set; }
    
    /// <summary>
    /// Authentication configuration
    /// </summary>
    public required AuthConfig Auth { get; set; }
    
    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    public RateLimitConfig RateLimit { get; set; } = new();
    
    /// <summary>
    /// List of skill file references
    /// </summary>
    public List<string> SkillFiles { get; set; } = new();
    
    /// <summary>
    /// Source OpenAPI spec URL
    /// </summary>
    public string? ImportedFrom { get; set; }
    
    /// <summary>
    /// When this manifest was created/updated
    /// </summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

public class ProviderInfo
{
    public required string Id { get; set; }       // gmail, hubspot, salesforce
    public required string Name { get; set; }     // Gmail, HubSpot, Salesforce
    public string? Icon { get; set; }             // gmail.png (relative path)
    public required string BaseUrl { get; set; }  // https://gmail.googleapis.com
    public string? DocsUrl { get; set; }          // Link to API documentation
}

public class AuthConfig
{
    /// <summary>
    /// Authentication type: oauth2, apikey, basic, bearer
    /// </summary>
    public required string Type { get; set; }
    
    // OAuth2 specific
    public string? AuthorizationUrl { get; set; }
    public string? TokenUrl { get; set; }
    public List<string> Scopes { get; set; } = new();
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }         // Direct storage (for local use)
    public string? ClientSecretRef { get; set; }      // Reference to encrypted vault secret
    
    // API Key specific
    public string? HeaderName { get; set; }       // Authorization, X-API-Key, etc.
    public string? HeaderPrefix { get; set; }     // Bearer, Basic, etc.
    public string? QueryParamName { get; set; }   // For query-based API keys
}

public class RateLimitConfig
{
    /// <summary>
    /// Maximum requests per minute (default: 60)
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;
    
    /// <summary>
    /// Initial backoff delay in milliseconds (default: 1000)
    /// </summary>
    public int BackoffInitialMs { get; set; } = 1000;
    
    /// <summary>
    /// Maximum backoff delay in milliseconds (default: 30000)
    /// </summary>
    public int BackoffMaxMs { get; set; } = 30000;
    
    /// <summary>
    /// Maximum retry attempts (default: 3)
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}





