using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using WpfRagApp.Services.ApiOrchestration.Models;

namespace WpfRagApp.Services.ApiOrchestration.Ingestion;

/// <summary>
/// OpenAPI 3.0/3.1 and Swagger 2.0 parser.
/// Extracts endpoints and converts them to UniversalApiSkill objects.
/// </summary>
public class OpenApiParser : IOpenApiParser
{
    private readonly HttpClient _httpClient;
    private const int MaxDescriptionLength = 200;
    
    public OpenApiParser(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }
    
    public async Task<OpenApiParseResult> ParseFromUrlAsync(string url, string providerId)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(url);
            var result = await ParseAsync(json, providerId);
            result.SourceUrl = url;
            return result;
        }
        catch (Exception ex)
        {
            return OpenApiParseResult.Fail($"Failed to fetch spec from URL: {ex.Message}");
        }
    }
    
    public string DetectVersion(string jsonSpec)
    {
        try
        {
            var doc = JsonNode.Parse(jsonSpec);
            if (doc == null) return "unknown";
            
            // OpenAPI 3.x
            var openapi = doc["openapi"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(openapi)) return openapi;
            
            // Swagger 2.0
            var swagger = doc["swagger"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(swagger)) return swagger;
            
            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
    
    public async Task<OpenApiParseResult> ParseAsync(string jsonSpec, string providerId)
    {
        try
        {
            var doc = JsonNode.Parse(jsonSpec);
            if (doc == null)
            {
                return OpenApiParseResult.Fail("Invalid JSON");
            }
            
            var version = DetectVersion(jsonSpec);
            var result = new OpenApiParseResult
            {
                Success = true,
                Version = version
            };
            
            // Extract provider info
            result.Provider = ExtractProviderInfo(doc, providerId);
            
            // Extract security schemes
            result.Auth = ExtractAuthConfig(doc);
            
            // Extract paths and operations
            var paths = doc["paths"]?.AsObject();
            if (paths == null)
            {
                return OpenApiParseResult.Fail("No paths found in spec");
            }
            
            foreach (var (path, pathItem) in paths)
            {
                if (pathItem == null) continue;
                
                var pathObj = pathItem.AsObject();
                foreach (var (method, operation) in pathObj)
                {
                    // Skip non-HTTP methods (like parameters, $ref, etc.)
                    if (!IsHttpMethod(method)) continue;
                    if (operation == null) continue;
                    
                    var skill = ExtractSkill(
                        path, 
                        method, 
                        operation, 
                        result.Provider, 
                        result.Auth,
                        doc
                    );
                    
                    if (skill != null)
                    {
                        result.Skills.Add(skill);
                    }
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return OpenApiParseResult.Fail($"Parse error: {ex.Message}");
        }
    }
    
    private ProviderInfo ExtractProviderInfo(JsonNode doc, string providerId)
    {
        var info = doc["info"];
        var servers = doc["servers"]?.AsArray();
        
        var baseUrl = servers?.FirstOrDefault()?["url"]?.GetValue<string>() ?? "";
        
        // Handle Swagger 2.0 format
        if (string.IsNullOrEmpty(baseUrl))
        {
            var host = doc["host"]?.GetValue<string>();
            var basePath = doc["basePath"]?.GetValue<string>() ?? "";
            var scheme = doc["schemes"]?.AsArray()?.FirstOrDefault()?.GetValue<string>() ?? "https";
            
            if (!string.IsNullOrEmpty(host))
            {
                baseUrl = $"{scheme}://{host}{basePath}";
            }
        }
        
        return new ProviderInfo
        {
            Id = providerId,
            Name = info?["title"]?.GetValue<string>() ?? providerId,
            BaseUrl = baseUrl,
            DocsUrl = info?["termsOfService"]?.GetValue<string>()
        };
    }
    
    private AuthConfig? ExtractAuthConfig(JsonNode doc)
    {
        // OpenAPI 3.x
        var securitySchemes = doc["components"]?["securitySchemes"]?.AsObject();
        
        // Swagger 2.0
        securitySchemes ??= doc["securityDefinitions"]?.AsObject();
        
        if (securitySchemes == null) return null;
        
        foreach (var (name, scheme) in securitySchemes)
        {
            if (scheme == null) continue;
            
            var type = scheme["type"]?.GetValue<string>()?.ToLower();
            
            switch (type)
            {
                case "oauth2":
                    return ExtractOAuth2Config(scheme);
                    
                case "apikey":
                    return new AuthConfig
                    {
                        Type = "apikey",
                        HeaderName = scheme["name"]?.GetValue<string>() ?? "X-API-Key",
                        HeaderPrefix = ""
                    };
                    
                case "http":
                    var httpScheme = scheme["scheme"]?.GetValue<string>()?.ToLower();
                    if (httpScheme == "bearer")
                    {
                        return new AuthConfig
                        {
                            Type = "bearer",
                            HeaderName = "Authorization",
                            HeaderPrefix = "Bearer"
                        };
                    }
                    break;
            }
        }
        
        return null;
    }
    
    private AuthConfig ExtractOAuth2Config(JsonNode scheme)
    {
        var config = new AuthConfig { Type = "oauth2" };
        
        // OpenAPI 3.x flows
        var flows = scheme["flows"]?.AsObject();
        if (flows != null)
        {
            // Try different flow types
            var flow = flows["authorizationCode"] 
                       ?? flows["implicit"] 
                       ?? flows["clientCredentials"] 
                       ?? flows["password"];
            
            if (flow != null)
            {
                config.AuthorizationUrl = flow["authorizationUrl"]?.GetValue<string>();
                config.TokenUrl = flow["tokenUrl"]?.GetValue<string>();
                
                var scopes = flow["scopes"]?.AsObject();
                if (scopes != null)
                {
                    config.Scopes = scopes.Select(s => s.Key).ToList();
                }
            }
        }
        
        // Swagger 2.0 format
        else
        {
            config.AuthorizationUrl = scheme["authorizationUrl"]?.GetValue<string>();
            config.TokenUrl = scheme["tokenUrl"]?.GetValue<string>();
            
            var scopes = scheme["scopes"]?.AsObject();
            if (scopes != null)
            {
                config.Scopes = scopes.Select(s => s.Key).ToList();
            }
        }
        
        return config;
    }
    
    private UniversalApiSkill? ExtractSkill(
        string path, 
        string method, 
        JsonNode operation,
        ProviderInfo provider,
        AuthConfig? authConfig,
        JsonNode fullDoc)
    {
        var operationId = operation["operationId"]?.GetValue<string>();
        var summary = operation["summary"]?.GetValue<string>();
        var description = operation["description"]?.GetValue<string>();
        
        // Generate operationId if not present
        if (string.IsNullOrEmpty(operationId))
        {
            operationId = GenerateOperationId(path, method);
        }
        
        var skillId = $"{provider.Id}_{SanitizeId(operationId)}";
        
        return new UniversalApiSkill
        {
            SkillId = skillId,
            ProviderId = provider.Id,
            DisplayName = summary ?? operationId,
            
            Endpoint = new ApiEndpoint
            {
                Method = method.ToUpper(),
                Path = path,
                BaseUrl = provider.BaseUrl
            },
            
            Parameters = ExtractParameters(operation, fullDoc),
            RequestBody = ExtractRequestBody(operation, fullDoc),
            
            Security = authConfig != null ? new ApiSecurity
            {
                Type = authConfig.Type,
                Scopes = ExtractOperationScopes(operation) ?? authConfig.Scopes,
                HeaderName = authConfig.HeaderName,
                HeaderPrefix = authConfig.HeaderPrefix
            } : null,
            
            Metadata = new SkillMetadata
            {
                SummaryForLLM = TruncateDescription(summary ?? description, MaxDescriptionLength),
                OriginalDescription = description,
                Tags = ExtractTags(operation),
                ImportedAt = DateTime.UtcNow
            }
        };
    }
    
    private List<ApiParameter> ExtractParameters(JsonNode operation, JsonNode fullDoc)
    {
        var parameters = new List<ApiParameter>();
        var paramsArray = operation["parameters"]?.AsArray();
        
        if (paramsArray == null) return parameters;
        
        foreach (var param in paramsArray)
        {
            if (param == null) continue;
            
            // Resolve $ref if present
            var resolvedParam = ResolveRef(param, fullDoc) ?? param;
            
            parameters.Add(new ApiParameter
            {
                Name = resolvedParam["name"]?.GetValue<string>() ?? "unknown",
                In = resolvedParam["in"]?.GetValue<string>() ?? "query",
                Type = ExtractType(resolvedParam["schema"] ?? resolvedParam),
                Required = resolvedParam["required"]?.GetValue<bool>() ?? false,
                Default = resolvedParam["default"]?.ToString(),
                Description = TruncateDescription(
                    resolvedParam["description"]?.GetValue<string>(), 
                    100
                ),
                Format = (resolvedParam["schema"] ?? resolvedParam)?["format"]?.GetValue<string>()
            });
        }
        
        return parameters;
    }
    
    private ApiRequestBody? ExtractRequestBody(JsonNode operation, JsonNode fullDoc)
    {
        var requestBody = operation["requestBody"];
        if (requestBody == null) return null;
        
        // Resolve $ref if present
        requestBody = ResolveRef(requestBody, fullDoc) ?? requestBody;
        
        var content = requestBody["content"]?.AsObject();
        if (content == null) return null;
        
        // Prefer JSON content
        var jsonContent = content["application/json"] ?? content.FirstOrDefault().Value;
        if (jsonContent == null) return null;
        
        var schema = jsonContent["schema"]?.AsObject();
        
        return new ApiRequestBody
        {
            ContentType = content.FirstOrDefault().Key ?? "application/json",
            Required = requestBody["required"]?.GetValue<bool>() ?? false,
            Description = TruncateDescription(
                requestBody["description"]?.GetValue<string>(), 
                100
            ),
            Schema = schema != null 
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(schema.ToJsonString())
                : null
        };
    }
    
    private JsonNode? ResolveRef(JsonNode node, JsonNode fullDoc)
    {
        var refPath = node["$ref"]?.GetValue<string>();
        if (string.IsNullOrEmpty(refPath)) return null;
        
        // Parse #/components/... or #/definitions/...
        var parts = refPath.TrimStart('#', '/').Split('/');
        
        JsonNode? current = fullDoc;
        foreach (var part in parts)
        {
            current = current?[part];
            if (current == null) break;
        }
        
        return current;
    }
    
    private string ExtractType(JsonNode? schema)
    {
        if (schema == null) return "string";
        
        var type = schema["type"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(type)) return type;
        
        // Check for $ref
        if (schema["$ref"] != null) return "object";
        
        // Check for array items
        if (schema["items"] != null) return "array";
        
        return "string";
    }
    
    private List<string>? ExtractOperationScopes(JsonNode operation)
    {
        var security = operation["security"]?.AsArray();
        if (security == null || !security.Any()) return null;
        
        var scopes = new List<string>();
        foreach (var requirement in security)
        {
            if (requirement == null) continue;
            
            var reqObj = requirement.AsObject();
            foreach (var (_, scopeArray) in reqObj)
            {
                if (scopeArray is JsonArray arr)
                {
                    scopes.AddRange(arr.Select(s => s?.GetValue<string>() ?? "").Where(s => !string.IsNullOrEmpty(s)));
                }
            }
        }
        
        return scopes.Any() ? scopes : null;
    }
    
    private List<string> ExtractTags(JsonNode operation)
    {
        var tags = operation["tags"]?.AsArray();
        if (tags == null) return new List<string>();
        
        return tags
            .Select(t => t?.GetValue<string>())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList()!;
    }
    
    private static bool IsHttpMethod(string method)
    {
        var httpMethods = new[] { "get", "post", "put", "patch", "delete", "head", "options" };
        return httpMethods.Contains(method.ToLower());
    }
    
    private static string GenerateOperationId(string path, string method)
    {
        // Convert /users/{id}/orders to users_id_orders_get
        var sanitized = path
            .Replace("{", "")
            .Replace("}", "")
            .Replace("/", "_")
            .Trim('_');
        
        return $"{sanitized}_{method}".ToLower();
    }
    
    private static string SanitizeId(string id)
    {
        return id
            .Replace(".", "_")
            .Replace("-", "_")
            .Replace(" ", "_")
            .ToLower();
    }
    
    private static string? TruncateDescription(string? description, int maxLength)
    {
        if (string.IsNullOrEmpty(description)) return null;
        
        if (description.Length <= maxLength) return description;
        
        return description[..(maxLength - 3)] + "...";
    }
}





