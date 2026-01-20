using Microsoft.Data.Sqlite;
using Dapper;
using System.Text.Json;
using WpfRagApp.Services.ApiOrchestration.Models;

namespace WpfRagApp.Services.ApiOrchestration;

/// <summary>
/// SQLite-based skill vector service with in-memory cosine similarity.
/// Self-contained solution - no external server required.
/// </summary>
public class SkillVectorService : ISkillVectorService
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    
    // Cache for loaded skills (for RAG performance)
    private readonly Dictionary<string, (UniversalApiSkill Skill, float[] Embedding)> _skillCache = new();
    private bool _cacheLoaded = false;
    
    public SkillVectorService(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rail", "vectors", "skills.db"
        );
        
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        _connectionString = $"Data Source={_dbPath}";
    }
    
    public async Task InitializeAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS skill_vectors (
                skill_id TEXT PRIMARY KEY,
                provider_id TEXT NOT NULL,
                skill_json TEXT NOT NULL,
                embedding BLOB NOT NULL,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE INDEX IF NOT EXISTS idx_skills_provider 
            ON skill_vectors(provider_id);
        ");
        
        await LoadCacheAsync();
    }
    
    private async Task LoadCacheAsync()
    {
        if (_cacheLoaded) return;
        
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            
            var rows = await conn.QueryAsync<dynamic>("SELECT skill_id, skill_json, embedding FROM skill_vectors");
            
            foreach (var row in rows)
            {
                try
                {
                    // Explicit string casts for dynamic properties
                    string skillId = row.skill_id?.ToString() ?? "";
                    string skillJsonStr = row.skill_json?.ToString() ?? "";
                    byte[] embeddingBytes = row.embedding as byte[] ?? Array.Empty<byte>();
                    
                    if (string.IsNullOrEmpty(skillJsonStr)) continue;
                    
                    var skill = JsonSerializer.Deserialize<UniversalApiSkill>(skillJsonStr);
                    var embedding = BytesToFloatArray(embeddingBytes);
                    
                    if (skill != null && !string.IsNullOrEmpty(skillId))
                    {
                        _skillCache[skillId] = (skill, embedding);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VectorService] Error loading skill: {ex.Message}");
                }
            }
            
            _cacheLoaded = true;
            System.Diagnostics.Debug.WriteLine($"[VectorService] Loaded {_skillCache.Count} skills into cache");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VectorService] Cache load error: {ex.Message}");
            _cacheLoaded = true; // Mark as loaded to prevent infinite retry
        }
    }
    
    public async Task UpsertSkillAsync(UniversalApiSkill skill, float[] embedding)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var skillJson = JsonSerializer.Serialize(skill);
        var embeddingBytes = FloatArrayToBytes(embedding);
        
        // Parameter names must match exactly (case-sensitive)
        await conn.ExecuteAsync(@"
            INSERT INTO skill_vectors (skill_id, provider_id, skill_json, embedding, updated_at)
            VALUES (@SkillId, @ProviderId, @SkillJson, @EmbeddingBytes, datetime('now'))
            ON CONFLICT(skill_id) DO UPDATE SET
                skill_json = @SkillJson,
                embedding = @EmbeddingBytes,
                updated_at = datetime('now')
        ", new { 
            SkillId = skill.SkillId, 
            ProviderId = skill.ProviderId, 
            SkillJson = skillJson, 
            EmbeddingBytes = embeddingBytes 
        });
        
        // Update cache
        _skillCache[skill.SkillId] = (skill, embedding);
    }
    
    public async Task<List<SkillSearchResult>> SearchAsync(float[] queryEmbedding, string? providerId = null, int topK = 5)
    {
        await LoadCacheAsync();
        
        var results = new List<(string SkillId, float Score)>();
        
        foreach (var (skillId, (skill, embedding)) in _skillCache)
        {
            // Filter by provider if specified
            if (providerId != null && skill.ProviderId != providerId)
            {
                continue;
            }
            
            // Calculate cosine similarity
            var similarity = CosineSimilarity(queryEmbedding, embedding);
            results.Add((skillId, similarity));
        }
        
        // Sort by similarity descending and take top K
        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .Where(r => r.Score > 0.3f) // Minimum threshold
            .Select(r => new SkillSearchResult
            {
                Skill = _skillCache[r.SkillId].Skill,
                Score = r.Score
            })
            .ToList();
    }
    
    public async Task<UniversalApiSkill?> GetSkillAsync(string skillId)
    {
        await LoadCacheAsync();
        
        if (_skillCache.TryGetValue(skillId, out var entry))
        {
            return entry.Skill;
        }
        
        return null;
    }
    
    public async Task DeleteProviderSkillsAsync(string providerId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        await conn.ExecuteAsync(
            "DELETE FROM skill_vectors WHERE provider_id = @providerId",
            new { providerId }
        );
        
        // Update cache
        var toRemove = _skillCache
            .Where(kv => kv.Value.Skill.ProviderId == providerId)
            .Select(kv => kv.Key)
            .ToList();
        
        foreach (var key in toRemove)
        {
            _skillCache.Remove(key);
        }
    }
    
    public async Task<List<UniversalApiSkill>> GetProviderSkillsAsync(string providerId)
    {
        await LoadCacheAsync();
        
        return _skillCache.Values
            .Where(entry => entry.Skill.ProviderId == providerId)
            .Select(entry => entry.Skill)
            .ToList();
    }
    
    #region Vector Math
    
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        
        float dotProduct = 0;
        float normA = 0;
        float normB = 0;
        
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        if (normA == 0 || normB == 0) return 0;
        
        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
    
    private static byte[] FloatArrayToBytes(float[] array)
    {
        var bytes = new byte[array.Length * sizeof(float)];
        Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
        return bytes;
    }
    
    private static float[] BytesToFloatArray(byte[] bytes)
    {
        var array = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
        return array;
    }
    
    #endregion
}





