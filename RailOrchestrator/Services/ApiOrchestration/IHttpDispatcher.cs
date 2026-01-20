using WpfRagApp.Services.ApiOrchestration.Models;

namespace WpfRagApp.Services.ApiOrchestration;

/// <summary>
/// Interface for HTTP API dispatcher.
/// Executes API calls with authentication and retry logic.
/// </summary>
public interface IHttpDispatcher
{
    /// <summary>
    /// Execute an API skill with the given parameters.
    /// Handles authentication, retry, and error handling.
    /// </summary>
    /// <param name="skill">The API skill definition</param>
    /// <param name="parameters">Parameter values (path, query, body)</param>
    /// <param name="userId">User ID for credential lookup</param>
    /// <returns>API response or error</returns>
    Task<ApiResponse> ExecuteAsync(UniversalApiSkill skill, Dictionary<string, object> parameters, string userId);
    
    /// <summary>
    /// Execute a raw HTTP request with authentication.
    /// </summary>
    Task<ApiResponse> ExecuteRawAsync(string method, string url, Dictionary<string, string>? headers,
        object? body, string userId, string providerId);
}

/// <summary>
/// API response wrapper with standard error handling.
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string? Data { get; set; }
    public ApiError? Error { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    
    public static ApiResponse Ok(string data, int statusCode = 200) => new()
    {
        Success = true,
        StatusCode = statusCode,
        Data = data
    };
    
    public static ApiResponse Fail(string message, int statusCode = 500, bool retryable = false) => new()
    {
        Success = false,
        StatusCode = statusCode,
        Error = new ApiError
        {
            Code = statusCode.ToString(),
            Message = message,
            Retryable = retryable
        }
    };
}

public class ApiError
{
    public required string Code { get; set; }
    public required string Message { get; set; }
    public bool Retryable { get; set; }
    public string? Details { get; set; }
}





