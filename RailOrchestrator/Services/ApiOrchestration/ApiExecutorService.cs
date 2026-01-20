using WpfRagApp.Services.ApiOrchestration.Ingestion;
using WpfRagApp.Services.ApiOrchestration.Models;
using WpfRagApp.Services.Vault;

namespace WpfRagApp.Services.ApiOrchestration;

public class ApiExecutorService : IApiExecutorService
{
    private readonly ISkillVectorService _vectorService;
    private readonly IHttpDispatcher _httpDispatcher;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVaultService _vaultService;
    
    private const string DefaultUserId = "default";
    
    public ApiExecutorService(
        ISkillVectorService vectorService,
        IHttpDispatcher httpDispatcher,
        IEmbeddingService embeddingService,
        IVaultService vaultService)
    {
        _vectorService = vectorService;
        _httpDispatcher = httpDispatcher;
        _embeddingService = embeddingService;
        _vaultService = vaultService;
    }
    
    public async Task<ApiExecutionResult> ExecuteByQueryAsync(
        string userQuery, 
        Dictionary<string, object>? parameters = null,
        string? userId = null,
        string? providerId = null)
    {
        try
        {
            var skills = await FindSkillsAsync(userQuery, providerId, topK: 1);
            if (!skills.Any())
            {
                return new ApiExecutionResult { Success = false, Error = "No matching API skill found for your request" };
            }
            var bestMatch = skills.First();
            return await ExecuteSkillAsync(bestMatch.Skill, parameters ?? new Dictionary<string, object>(), userId ?? DefaultUserId);
        }
        catch (Exception ex)
        {
            return new ApiExecutionResult { Success = false, Error = $"Execution error: {ex.Message}" };
        }
    }
    
    public async Task<ApiExecutionResult> ExecuteBySkillIdAsync(string skillId, Dictionary<string, object> parameters, string? userId = null)
    {
        try
        {
            var skill = await _vectorService.GetSkillAsync(skillId);
            if (skill == null)
            {
                return new ApiExecutionResult { Success = false, Error = $"Skill not found: {skillId}" };
            }
            return await ExecuteSkillAsync(skill, parameters, userId ?? DefaultUserId);
        }
        catch (Exception ex)
        {
            return new ApiExecutionResult { Success = false, Error = $"Execution error: {ex.Message}" };
        }
    }
    
    private async Task<ApiExecutionResult> ExecuteSkillAsync(UniversalApiSkill skill, Dictionary<string, object> parameters, string userId)
    {
        var isConnected = await _vaultService.IsConnectedAsync(userId, skill.ProviderId);
        if (!isConnected && skill.Security?.Type != "none" && skill.Security != null)
        {
            return new ApiExecutionResult
            {
                Success = false,
                SkillId = skill.SkillId,
                SkillName = skill.DisplayName,
                Error = $"API '{skill.ProviderId}' not connected. Please configure in settings."
            };
        }
        var response = await _httpDispatcher.ExecuteAsync(skill, parameters, userId);
        return new ApiExecutionResult
        {
            Success = response.Success,
            SkillId = skill.SkillId,
            SkillName = skill.DisplayName,
            Data = response.Data,
            Error = response.Error?.Message,
            StatusCode = response.StatusCode
        };
    }
    
    public async Task<List<SkillMatch>> FindSkillsAsync(string query, string? providerId = null, int topK = 3)
    {
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
        var results = await _vectorService.SearchAsync(queryEmbedding, providerId, topK);
        return results.Select(r => new SkillMatch { Skill = r.Skill, Score = r.Score }).ToList();
    }
    
    public ToolDefinition GetToolDefinition()
    {
        return new ToolDefinition
        {
            Name = "execute_api",
            Description = "Execute an external API call to connected services (Gmail, HubSpot, etc.)",
            Parameters = new Dictionary<string, ParameterSchema>
            {
                ["skill_id"] = new ParameterSchema { Type = "string", Description = "The ID of the API skill to execute", Required = true },
                ["parameters"] = new ParameterSchema { Type = "object", Description = "Parameters for the API call", Required = false }
            }
        };
    }
    
    public async Task<string> GetContextForQueryAsync(string query, string? providerId = null)
    {
        var skills = await FindSkillsAsync(query, providerId, topK: 3);
        if (!skills.Any()) return "No matching API skills found.";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Available API Skills:");
        sb.AppendLine();
        foreach (var match in skills)
        {
            sb.AppendLine(match.FormatForContext());
            sb.AppendLine("---");
        }
        return sb.ToString();
    }
}





