using WpfRagApp.Services.ApiOrchestration.Models;

namespace WpfRagApp.Services.ApiOrchestration;

/// <summary>
/// Interface for skill vector search service.
/// Provides semantic search over API skills using embeddings.
/// </summary>
public interface ISkillVectorService
{
    /// <summary>
    /// Initialize the vector database.
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Add or update a skill in the vector index.
    /// </summary>
    Task UpsertSkillAsync(UniversalApiSkill skill, float[] embedding);
    
    /// <summary>
    /// Search for skills semantically similar to the query.
    /// </summary>
    /// <param name="queryEmbedding">Embedding vector of the search query</param>
    /// <param name="providerId">Optional: filter by provider</param>
    /// <param name="topK">Number of results to return</param>
    Task<List<SkillSearchResult>> SearchAsync(float[] queryEmbedding, string? providerId = null, int topK = 5);
    
    /// <summary>
    /// Get a skill by ID.
    /// </summary>
    Task<UniversalApiSkill?> GetSkillAsync(string skillId);
    
    /// <summary>
    /// Delete all skills for a provider.
    /// </summary>
    Task DeleteProviderSkillsAsync(string providerId);
    
    /// <summary>
    /// Get all skills for a provider.
    /// </summary>
    Task<List<UniversalApiSkill>> GetProviderSkillsAsync(string providerId);
}

/// <summary>
/// Result from a skill search operation.
/// </summary>
public class SkillSearchResult
{
    public required UniversalApiSkill Skill { get; set; }
    public float Score { get; set; }  // Cosine similarity score (0-1)
}





