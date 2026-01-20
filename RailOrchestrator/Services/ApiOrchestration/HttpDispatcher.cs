using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WpfRagApp.Services.ApiOrchestration.Models;
using WpfRagApp.Services.Vault;

namespace WpfRagApp.Services.ApiOrchestration;

/// <summary>
/// HTTP API dispatcher with authentication injection and retry logic.
/// Implements exponential backoff for rate limiting and transient errors.
/// </summary>
public class HttpDispatcher : IHttpDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly IVaultService _vault;
    private readonly Dictionary<string, RateLimitConfig> _rateLimitConfigs = new();
    
    public HttpDispatcher(IVaultService vault, HttpClient? httpClient = null)
    {
        _vault = vault;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }
    
    public async Task<ApiResponse> ExecuteAsync(UniversalApiSkill skill, 
        Dictionary<string, object> parameters, string userId)
    {
        try
        {
            // Build URL with path and query parameters
            var url = BuildUrl(skill.Endpoint, parameters);
            
            // Build request body
            object? body = null;
            if (skill.RequestBody != null && parameters.TryGetValue("body", out var bodyParam))
            {
                body = bodyParam;
            }
            
            // Get rate limit config
            var rateLimitConfig = GetRateLimitConfig(skill.ProviderId);
            
            // Execute with retry
            return await ExecuteWithRetryAsync(
                skill.Endpoint.Method,
                url,
                body,
                userId,
                skill.ProviderId,
                skill.Security,
                rateLimitConfig
            );
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"Execution error: {ex.Message}");
        }
    }
    
    public async Task<ApiResponse> ExecuteRawAsync(string method, string url, 
        Dictionary<string, string>? headers, object? body, string userId, string providerId)
    {
        var rateLimitConfig = GetRateLimitConfig(providerId);
        return await ExecuteWithRetryAsync(method, url, body, userId, providerId, null, rateLimitConfig, headers);
    }
    
    private async Task<ApiResponse> ExecuteWithRetryAsync(
        string method, 
        string url, 
        object? body,
        string userId,
        string providerId,
        ApiSecurity? security,
        RateLimitConfig rateLimitConfig,
        Dictionary<string, string>? additionalHeaders = null)
    {
        int retries = 0;
        Exception? lastException = null;
        
        while (retries <= rateLimitConfig.MaxRetries)
        {
            try
            {
                // Build request
                var request = new HttpRequestMessage(new HttpMethod(method.ToUpper()), url);
                
                // Add authentication header
                await AddAuthHeaderAsync(request, userId, providerId, security);
                
                // Add additional headers
                if (additionalHeaders != null)
                {
                    foreach (var (key, value) in additionalHeaders)
                    {
                        request.Headers.TryAddWithoutValidation(key, value);
                    }
                }
                
                // Add body
                if (body != null)
                {
                    var json = body is string s ? s : JsonSerializer.Serialize(body);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                
                // Execute
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Handle response
                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = ApiResponse.Ok(responseContent, (int)response.StatusCode);
                    foreach (var header in response.Headers)
                    {
                        apiResponse.Headers[header.Key] = string.Join(", ", header.Value);
                    }
                    return apiResponse;
                }
                
                // Handle specific error codes
                switch (response.StatusCode)
                {
                    case HttpStatusCode.TooManyRequests: // 429
                        var delay = CalculateBackoff(retries, rateLimitConfig);
                        await Task.Delay(delay);
                        retries++;
                        continue;
                        
                    case HttpStatusCode.Unauthorized: // 401
                        // Try to refresh token
                        var refreshed = await _vault.RefreshOAuth2Async(userId, providerId);
                        if (refreshed != null)
                        {
                            retries++;
                            continue;
                        }
                        return ApiResponse.Fail("Authentication expired. Please reconnect.", 401);
                        
                    case HttpStatusCode.Forbidden: // 403
                        return ApiResponse.Fail("Access denied. Missing permissions.", 403);
                        
                    case HttpStatusCode.InternalServerError: // 500
                    case HttpStatusCode.BadGateway: // 502
                    case HttpStatusCode.ServiceUnavailable: // 503
                    case HttpStatusCode.GatewayTimeout: // 504
                        var serverDelay = CalculateBackoff(retries, rateLimitConfig);
                        await Task.Delay(serverDelay);
                        retries++;
                        continue;
                        
                    default:
                        return ApiResponse.Fail($"API Error: {response.StatusCode} - {responseContent}", 
                            (int)response.StatusCode);
                }
            }
            catch (TaskCanceledException)
            {
                return ApiResponse.Fail("Request timeout", 408, retryable: true);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                var networkDelay = CalculateBackoff(retries, rateLimitConfig);
                await Task.Delay(networkDelay);
                retries++;
            }
        }
        
        return ApiResponse.Fail($"Max retries exceeded. Last error: {lastException?.Message}", 
            500, retryable: false);
    }
    
    private async Task AddAuthHeaderAsync(HttpRequestMessage request, string userId, 
        string providerId, ApiSecurity? security)
    {
        if (security == null) return;
        
        switch (security.Type.ToLower())
        {
            case "oauth2":
            case "bearer":
                var oauth = await _vault.GetOAuth2Async(userId, providerId);
                if (oauth != null)
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        oauth.TokenType, oauth.AccessToken);
                }
                break;
                
            case "apikey":
                var apiKey = await _vault.GetApiKeyAsync(userId, providerId);
                if (apiKey != null)
                {
                    var value = string.IsNullOrEmpty(apiKey.Prefix) 
                        ? apiKey.Key 
                        : $"{apiKey.Prefix} {apiKey.Key}";
                    request.Headers.TryAddWithoutValidation(apiKey.HeaderName, value);
                }
                break;
                
            case "basic":
                // Basic auth would be handled here
                break;
        }
    }
    
    private string BuildUrl(ApiEndpoint endpoint, Dictionary<string, object> parameters)
    {
        var path = endpoint.Path;
        var queryParams = new List<string>();
        
        foreach (var (key, value) in parameters)
        {
            if (key == "body") continue;  // Skip body
            
            var placeholder = $"{{{key}}}";
            if (path.Contains(placeholder))
            {
                // Path parameter
                path = path.Replace(placeholder, Uri.EscapeDataString(value?.ToString() ?? ""));
            }
            else
            {
                // Query parameter
                queryParams.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value?.ToString() ?? "")}");
            }
        }
        
        var url = $"{endpoint.BaseUrl.TrimEnd('/')}{path}";
        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }
        
        return url;
    }
    
    private int CalculateBackoff(int attempt, RateLimitConfig config)
    {
        // Exponential backoff with jitter
        var delay = config.BackoffInitialMs * (int)Math.Pow(2, attempt);
        var jitter = Random.Shared.Next(0, delay / 10);
        return Math.Min(delay + jitter, config.BackoffMaxMs);
    }
    
    private RateLimitConfig GetRateLimitConfig(string providerId)
    {
        if (_rateLimitConfigs.TryGetValue(providerId, out var config))
        {
            return config;
        }
        
        // Return default config
        return new RateLimitConfig();
    }
    
    /// <summary>
    /// Set custom rate limit config for a provider.
    /// </summary>
    public void SetRateLimitConfig(string providerId, RateLimitConfig config)
    {
        _rateLimitConfigs[providerId] = config;
    }
}





