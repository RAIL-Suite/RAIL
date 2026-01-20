namespace WpfRagApp.Services.ApiOrchestration.Ingestion;

/// <summary>
/// Interface for generating text embeddings.
/// Used for semantic search over API skills.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embedding vector for a text string.
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text);
    
    /// <summary>
    /// Generate embeddings for multiple texts in batch.
    /// </summary>
    Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts);
}





