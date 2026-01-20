namespace WpfRagApp.Services.ApiOrchestration.Models;

/// <summary>
/// Represents a single API operation normalized from OpenAPI/Swagger spec.
/// Used for RAG retrieval and LLM context injection.
/// </summary>
public class UniversalApiSkill
{
    /// <summary>
    /// Unique identifier: {provider}_{operationId}
    /// Example: "gmail_drafts_create"
    /// </summary>
    public required string SkillId { get; set; }
    
    /// <summary>
    /// Provider identifier (gmail, hubspot, salesforce, etc.)
    /// </summary>
    public required string ProviderId { get; set; }
    
    /// <summary>
    /// Human-friendly name for display
    /// </summary>
    public required string DisplayName { get; set; }
    
    /// <summary>
    /// HTTP endpoint details
    /// </summary>
    public required ApiEndpoint Endpoint { get; set; }
    
    /// <summary>
    /// List of parameters (path, query, header)
    /// </summary>
    public List<ApiParameter> Parameters { get; set; } = new();
    
    /// <summary>
    /// Request body schema (if applicable)
    /// </summary>
    public ApiRequestBody? RequestBody { get; set; }
    
    /// <summary>
    /// Security requirements for this endpoint
    /// </summary>
    public ApiSecurity? Security { get; set; }
    
    /// <summary>
    /// Metadata for RAG and LLM processing
    /// </summary>
    public SkillMetadata Metadata { get; set; } = new();
}

public class ApiEndpoint
{
    public required string Method { get; set; }  // GET, POST, PUT, DELETE, PATCH
    public required string Path { get; set; }     // /users/{userId}/drafts
    public required string BaseUrl { get; set; }  // https://gmail.googleapis.com
    
    public string FullUrl => $"{BaseUrl.TrimEnd('/')}{Path}";
}

public class ApiParameter
{
    public required string Name { get; set; }
    public required string In { get; set; }  // path, query, header, cookie
    public required string Type { get; set; } // string, integer, boolean, array, object
    public bool Required { get; set; }
    public string? Default { get; set; }
    public string? Description { get; set; }
    public string? Format { get; set; }  // date-time, email, uri, etc.
}

public class ApiRequestBody
{
    public string ContentType { get; set; } = "application/json";
    public bool Required { get; set; } = true;
    public Dictionary<string, object>? Schema { get; set; }
    public string? Description { get; set; }
}

public class ApiSecurity
{
    public required string Type { get; set; }  // oauth2, apikey, basic, bearer
    public List<string> Scopes { get; set; } = new();
    public string? HeaderName { get; set; }     // For API Key: X-API-Key, Authorization, etc.
    public string? HeaderPrefix { get; set; }   // Bearer, Basic, etc.
}

public class SkillMetadata
{
    /// <summary>
    /// Concise summary for LLM context (max 200 chars)
    /// </summary>
    public string? SummaryForLLM { get; set; }
    
    /// <summary>
    /// Full original description (for reference)
    /// </summary>
    public string? OriginalDescription { get; set; }
    
    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// When this skill was imported
    /// </summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Source URL of the OpenAPI spec
    /// </summary>
    public string? SourceUrl { get; set; }
}





