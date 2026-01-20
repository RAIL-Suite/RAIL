using System.Text.Json;
using WpfRagApp.Services.ApiOrchestration.Models;
using WpfRagApp.Services;

namespace WpfRagApp.Services.ApiOrchestration.Ingestion;

/// <summary>
/// Main API ingestion orchestrator.
/// Coordinates: Parsing → Embedding → Storage → Manifest creation.
/// </summary>
public class IngestionService : IIngestionService
{
    private readonly IOpenApiParser _parser;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISkillVectorService _vectorService;
    private readonly string _skillsBasePath;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public IngestionService(
        IOpenApiParser parser,
        IEmbeddingService embeddingService,
        ISkillVectorService vectorService,
        string? skillsBasePath = null)
    {
        _parser = parser;
        _embeddingService = embeddingService;
        _vectorService = vectorService;
        
        // Use unified assets path
        _skillsBasePath = skillsBasePath ?? AssetService.GetDefaultRootPath();
    }
    
    public async Task<IngestionResult> ImportFromUrlAsync(string url, string providerId, 
        IProgress<IngestionProgress>? progress = null)
    {
        try
        {
            progress?.Report(new IngestionProgress 
            { 
                Stage = "Fetching", 
                Message = $"Downloading spec from {url}..." 
            });
            
            var parseResult = await _parser.ParseFromUrlAsync(url, providerId);
            
            if (!parseResult.Success)
            {
                return IngestionResult.Fail(parseResult.Error ?? "Parse failed");
            }
            
            return await ProcessParsedSpecAsync(parseResult, providerId, url, progress);
        }
        catch (Exception ex)
        {
            return IngestionResult.Fail($"Import error: {ex.Message}");
        }
    }
    
    public async Task<IngestionResult> ImportFromJsonAsync(string json, string providerId, 
        IProgress<IngestionProgress>? progress = null)
    {
        try
        {
            progress?.Report(new IngestionProgress 
            { 
                Stage = "Parsing", 
                Message = "Parsing OpenAPI specification..." 
            });
            
            var parseResult = await _parser.ParseAsync(json, providerId);
            
            if (!parseResult.Success)
            {
                return IngestionResult.Fail(parseResult.Error ?? "Parse failed");
            }
            
            return await ProcessParsedSpecAsync(parseResult, providerId, null, progress);
        }
        catch (Exception ex)
        {
            return IngestionResult.Fail($"Import error: {ex.Message}");
        }
    }
    
    private async Task<IngestionResult> ProcessParsedSpecAsync(
        OpenApiParseResult parseResult, 
        string providerId,
        string? sourceUrl,
        IProgress<IngestionProgress>? progress)
    {
        var skills = parseResult.Skills;
        var total = skills.Count;
        
        if (total == 0)
        {
            return IngestionResult.Fail("No endpoints found in specification");
        }
        
        // Initialize vector service
        await _vectorService.InitializeAsync();
        
        // Delete existing skills for this provider
        await _vectorService.DeleteProviderSkillsAsync(providerId);
        
        // Generate embeddings and store skills
        progress?.Report(new IngestionProgress 
        { 
            Stage = "Embedding", 
            Current = 0, 
            Total = total,
            Message = "Generating embeddings..." 
        });
        
        // Prepare texts for batch embedding
        var textsForEmbedding = skills.Select(s => 
            $"{s.Endpoint.Method} {s.Endpoint.Path} - {s.Metadata.SummaryForLLM ?? s.DisplayName}"
        ).ToList();
        
        // Generate embeddings in batch for efficiency
        List<float[]> embeddings;
        try
        {
            embeddings = await _embeddingService.GenerateBatchEmbeddingsAsync(textsForEmbedding);
        }
        catch
        {
            // Fallback to individual embeddings if batch fails
            embeddings = new List<float[]>();
            for (int i = 0; i < textsForEmbedding.Count; i++)
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(textsForEmbedding[i]);
                embeddings.Add(embedding);
                
                progress?.Report(new IngestionProgress
                {
                    Stage = "Embedding",
                    Current = i + 1,
                    Total = total,
                    Message = $"Embedding skill {i + 1}/{total}"
                });
            }
        }
        
        // Store skills with embeddings
        progress?.Report(new IngestionProgress 
        { 
            Stage = "Storing", 
            Current = 0, 
            Total = total,
            Message = "Storing skills..." 
        });
        
        for (int i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            skill.Metadata.SourceUrl = sourceUrl;
            
            await _vectorService.UpsertSkillAsync(skill, embeddings[i]);
            
            progress?.Report(new IngestionProgress
            {
                Stage = "Storing",
                Current = i + 1,
                Total = total,
                Message = $"Stored {skill.SkillId}"
            });
        }
        
        // Create and save manifest
        progress?.Report(new IngestionProgress 
        { 
            Stage = "Finalizing", 
            Message = "Creating manifest..." 
        });
        
        var manifest = CreateManifest(parseResult, providerId, sourceUrl, skills);
        await SaveManifestAndSkillsAsync(providerId, manifest, skills);
        
        return new IngestionResult
        {
            Success = true,
            ProviderId = providerId,
            SkillCount = skills.Count,
            Manifest = manifest
        };
    }
    
    private ApiManifest CreateManifest(OpenApiParseResult parseResult, string providerId, 
        string? sourceUrl, List<UniversalApiSkill> skills)
    {
        return new ApiManifest
        {
            ManifestVersion = "2.0",
            Type = "api",
            Provider = parseResult.Provider ?? new ProviderInfo
            {
                Id = providerId,
                Name = providerId,
                BaseUrl = ""
            },
            Auth = parseResult.Auth ?? new AuthConfig { Type = "none" },
            RateLimit = new RateLimitConfig(),
            SkillFiles = skills.Select(s => $"skills/{s.SkillId}.json").ToList(),
            ImportedFrom = sourceUrl,
            ImportedAt = DateTime.UtcNow
        };
    }
    
    private async Task SaveManifestAndSkillsAsync(string providerId, ApiManifest manifest, 
        List<UniversalApiSkill> skills)
    {
        var providerPath = Path.Combine(_skillsBasePath, providerId);
        var skillsPath = Path.Combine(providerPath, "skills");
        
        // Create directories
        Directory.CreateDirectory(providerPath);
        Directory.CreateDirectory(skillsPath);
        
        // Save manifest as api.manifest.json (for unified asset detection)
        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(providerPath, "api.manifest.json"), manifestJson);
        
        // Save individual skill files
        foreach (var skill in skills)
        {
            var skillJson = JsonSerializer.Serialize(skill, JsonOptions);
            await File.WriteAllTextAsync(
                Path.Combine(skillsPath, $"{skill.SkillId}.json"), 
                skillJson
            );
        }
    }
    
    public async Task DeleteProviderAsync(string providerId)
    {
        // Delete from vector service
        await _vectorService.DeleteProviderSkillsAsync(providerId);
        
        // Delete from file system
        var providerPath = Path.Combine(_skillsBasePath, providerId);
        if (Directory.Exists(providerPath))
        {
            Directory.Delete(providerPath, recursive: true);
        }
    }
    
    public Task<List<string>> GetImportedProvidersAsync()
    {
        var providers = new List<string>();
        
        if (Directory.Exists(_skillsBasePath))
        {
            foreach (var dir in Directory.GetDirectories(_skillsBasePath))
            {
                var manifestPath = Path.Combine(dir, "api.manifest.json");
                if (File.Exists(manifestPath))
                {
                    providers.Add(Path.GetFileName(dir));
                }
            }
        }
        
        return Task.FromResult(providers);
    }
}





