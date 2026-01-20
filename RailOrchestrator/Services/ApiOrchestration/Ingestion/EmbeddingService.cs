using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace WpfRagApp.Services.ApiOrchestration.Ingestion;

/// <summary>
/// Embedding service using Gemini API.
/// Generates text embeddings for semantic search.
/// </summary>
public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    
    public GeminiEmbeddingService(string apiKey, string model = "text-embedding-004")
    {
        _apiKey = apiKey;
        _model = model;
        _httpClient = new HttpClient();
    }
    
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var url = $"{BaseUrl}/models/{_model}:embedContent?key={_apiKey}";
        
        var request = new
        {
            model = $"models/{_model}",
            content = new
            {
                parts = new[]
                {
                    new { text }
                }
            }
        };
        
        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
        
        return result?.Embedding?.Values ?? Array.Empty<float>();
    }
    
    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts)
    {
        var url = $"{BaseUrl}/models/{_model}:batchEmbedContents?key={_apiKey}";
        
        var requests = texts.Select(text => new
        {
            model = $"models/{_model}",
            content = new
            {
                parts = new[]
                {
                    new { text }
                }
            }
        }).ToArray();
        
        var request = new { requests };
        
        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<BatchEmbeddingResponse>();
        
        return result?.Embeddings?.Select(e => e.Values ?? Array.Empty<float>()).ToList() 
               ?? new List<float[]>();
    }
    
    #region Response DTOs
    
    private class EmbeddingResponse
    {
        public EmbeddingData? Embedding { get; set; }
    }
    
    private class BatchEmbeddingResponse
    {
        public List<EmbeddingData>? Embeddings { get; set; }
    }
    
    private class EmbeddingData
    {
        public float[]? Values { get; set; }
    }
    
    #endregion
}

/// <summary>
/// Simple local embedding service using hash-based vectors.
/// Used as fallback when API is not available.
/// </summary>
public class LocalEmbeddingService : IEmbeddingService
{
    private const int VectorSize = 768; // Match Gemini embedding size
    
    public Task<float[]> GenerateEmbeddingAsync(string text)
    {
        // Simple hash-based embedding (for testing/fallback only)
        var vector = new float[VectorSize];
        var words = text.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            var hash = word.GetHashCode();
            var index = Math.Abs(hash) % VectorSize;
            vector[index] += 1.0f;
        }
        
        // Normalize
        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }
        
        return Task.FromResult(vector);
    }
    
    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            results.Add(await GenerateEmbeddingAsync(text));
        }
        return results;
    }
}





