using WpfRagApp.Services.ApiOrchestration.Ingestion;
using WpfRagApp.Services.ApiOrchestration.Models;
using WpfRagApp.Services.Vault;

namespace WpfRagApp.Services.ApiOrchestration;

/// <summary>
/// Interface for the API executor - main runtime entry point.
/// Handles RAG retrieval, parameter filling, and execution.
/// </summary>
public interface IApiExecutorService
{
    /// <summary>
    /// Find and execute the best matching API skill for a user query.
    /// </summary>
    /// <param name="userQuery">Natural language query from user</param>
    /// <param name="parameters">Parameters extracted by LLM</param>
    /// <param name="userId">Current user ID for auth</param>
    /// <param name="providerId">Optional: limit to specific provider</param>
    Task<ApiExecutionResult> ExecuteByQueryAsync(
        string userQuery, 
        Dictionary<string, object>? parameters = null,
        string? userId = null,
        string? providerId = null);
    
    /// <summary>
    /// Execute a specific API skill by ID.
    /// </summary>
    Task<ApiExecutionResult> ExecuteBySkillIdAsync(
        string skillId,
        Dictionary<string, object> parameters,
        string? userId = null);
    
    /// <summary>
    /// Search for matching skills without executing.
    /// Returns skill definitions for LLM context injection.
    /// </summary>
    Task<List<SkillMatch>> FindSkillsAsync(string query, string? providerId = null, int topK = 3);
    
    /// <summary>
    /// Get tool definition for LLM/ReAct integration.
    /// </summary>
    ToolDefinition GetToolDefinition();
}

/// <summary>
/// Result of an API execution.
/// </summary>
public class ApiExecutionResult
{
    public bool Success { get; set; }
    public string? SkillId { get; set; }
    public string? SkillName { get; set; }
    public string? Data { get; set; }
    public string? Error { get; set; }
    public int StatusCode { get; set; }
    
    /// <summary>
    /// Formatted response for LLM consumption.
    /// </summary>
    public string FormatForLLM()
    {
        if (Success)
        {
            return $"✓ {SkillName}: {TruncateData(Data, 500)}";
        }
        return $"✗ {SkillName ?? "API call"} failed: {Error}";
    }
    
    private static string TruncateData(string? data, int maxLength)
    {
        if (string.IsNullOrEmpty(data)) return "(no data)";
        if (data.Length <= maxLength) return data;
        return data[..maxLength] + "...";
    }
}

/// <summary>
/// Skill match from RAG retrieval.
/// </summary>
public class SkillMatch
{
    public required UniversalApiSkill Skill { get; set; }
    public float Score { get; set; }
    
    /// <summary>
    /// Format skill as context for LLM injection.
    /// </summary>
    public string FormatForContext()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Skill: {Skill.DisplayName} ({Skill.SkillId})");
        sb.AppendLine($"Endpoint: {Skill.Endpoint.Method} {Skill.Endpoint.Path}");
        
        if (Skill.Parameters.Any())
        {
            sb.AppendLine("Parameters:");
            foreach (var p in Skill.Parameters)
            {
                var req = p.Required ? "*" : "";
                sb.AppendLine($"  - {p.Name}{req}: {p.Type} ({p.Description ?? "no description"})");
            }
        }
        
        if (Skill.RequestBody != null)
        {
            sb.AppendLine($"Body: {Skill.RequestBody.ContentType}");
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Tool definition for LLM/ReAct integration.
/// </summary>
public class ToolDefinition
{
    public string Name { get; set; } = "execute_api";
    public string Description { get; set; } = "Execute an external API call";
    public Dictionary<string, ParameterSchema> Parameters { get; set; } = new();
}

public class ParameterSchema
{
    public string Type { get; set; } = "string";
    public string? Description { get; set; }
    public bool Required { get; set; }
}





