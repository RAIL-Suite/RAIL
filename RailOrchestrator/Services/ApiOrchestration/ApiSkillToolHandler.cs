using System.Text.Json;
using WpfRagApp.Services.ApiOrchestration.Ingestion;
using WpfRagApp.Services.ApiOrchestration.Models;

namespace WpfRagApp.Services.ApiOrchestration;

/// <summary>
/// ReAct tool handler for API skill execution.
/// Bridges the LLM ReAct system with the API executor.
/// </summary>
public class ApiSkillToolHandler
{
    private readonly IApiExecutorService _executor;
    
    public ApiSkillToolHandler(IApiExecutorService executor)
    {
        _executor = executor;
    }
    
    /// <summary>
    /// Tool name for ReAct integration.
    /// </summary>
    public const string ToolName = "execute_api";
    
    /// <summary>
    /// Get the tool description for ReAct system prompt.
    /// </summary>
    public static string GetToolDescription()
    {
        return @"execute_api: Execute an external API call to connected services.
Parameters:
  - skill_id (required): The API skill ID to execute (e.g., 'gmail_drafts_create')
  - params (optional): JSON object with parameters for the API call
Example: execute_api(skill_id=""gmail_drafts_create"", params={""to"":""user@example.com"",""subject"":""Hello""})";
    }
    
    /// <summary>
    /// Handle tool execution from ReAct.
    /// </summary>
    public async Task<string> HandleAsync(string arguments, string userId = "default")
    {
        try
        {
            // Parse arguments (could be JSON or key=value format)
            var (skillId, parameters) = ParseArguments(arguments);
            
            if (string.IsNullOrEmpty(skillId))
            {
                return "Error: skill_id is required";
            }
            
            var result = await _executor.ExecuteBySkillIdAsync(skillId, parameters, userId);
            return result.FormatForLLM();
        }
        catch (Exception ex)
        {
            return $"Error executing API: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Handle semantic skill search for context injection.
    /// </summary>
    public async Task<string> SearchSkillsAsync(string query, string? providerId = null)
    {
        var skills = await _executor.FindSkillsAsync(query, providerId, topK: 3);
        
        if (!skills.Any())
        {
            return "No matching API skills found for your query.";
        }
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Found matching API skills:");
        sb.AppendLine();
        
        foreach (var match in skills)
        {
            sb.AppendLine($"- {match.Skill.SkillId}: {match.Skill.DisplayName}");
            sb.AppendLine($"  Endpoint: {match.Skill.Endpoint.Method} {match.Skill.Endpoint.Path}");
            sb.AppendLine($"  Score: {match.Score:F2}");
        }
        
        return sb.ToString();
    }
    
    private (string? skillId, Dictionary<string, object> parameters) ParseArguments(string arguments)
    {
        var parameters = new Dictionary<string, object>();
        string? skillId = null;
        
        // Try JSON format first
        if (arguments.TrimStart().StartsWith("{"))
        {
            try
            {
                var json = JsonDocument.Parse(arguments);
                var root = json.RootElement;
                
                if (root.TryGetProperty("skill_id", out var skillIdProp))
                {
                    skillId = skillIdProp.GetString();
                }
                
                if (root.TryGetProperty("params", out var paramsProp))
                {
                    parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsProp.GetRawText()) 
                                 ?? new Dictionary<string, object>();
                }
                else if (root.TryGetProperty("parameters", out var params2Prop))
                {
                    parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(params2Prop.GetRawText()) 
                                 ?? new Dictionary<string, object>();
                }
                
                return (skillId, parameters);
            }
            catch
            {
                // Fall through to key=value parsing
            }
        }
        
        // Parse key=value format: skill_id="value", params={...}
        var parts = arguments.Split(',');
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length != 2) continue;
            
            var key = keyValue[0].Trim();
            var value = keyValue[1].Trim().Trim('"');
            
            if (key == "skill_id")
            {
                skillId = value;
            }
            else if (key == "params" || key == "parameters")
            {
                try
                {
                    parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(value) 
                                 ?? new Dictionary<string, object>();
                }
                catch
                {
                    // Ignore parse errors
                }
            }
            else
            {
                // Add as parameter directly
                parameters[key] = value;
            }
        }
        
        return (skillId, parameters);
    }
}





