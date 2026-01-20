using WpfRagApp.Services.ApiOrchestration.Ingestion;
using WpfRagApp.Services.Vault;

namespace WpfRagApp.Services.ApiOrchestration;

/// <summary>
/// Factory for API orchestration services with lazy singleton pattern.
/// Services are initialized once on first access and reused for app lifetime.
/// </summary>
public static class ApiOrchestrationFactory
{
    // Lazy initialization lock
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static bool _isInitialized;
    private static string? _geminiApiKey;
    
    // Singleton service instances
    private static IVaultService? _vaultService;
    private static ISkillVectorService? _vectorService;
    private static IEmbeddingService? _embeddingService;
    private static IOpenApiParser? _openApiParser;
    private static IHttpDispatcher? _httpDispatcher;
    private static IIngestionService? _ingestionService;
    private static IApiExecutorService? _executorService;
    private static ApiSkillToolHandler? _toolHandler;
    
    /// <summary>
    /// Configure the API key for Gemini embeddings.
    /// Call this at app startup before any API operations.
    /// </summary>
    public static void Configure(string? geminiApiKey = null)
    {
        _geminiApiKey = geminiApiKey;
    }
    
    /// <summary>
    /// Ensure services are initialized. Safe to call multiple times.
    /// Thread-safe, will only initialize once.
    /// </summary>
    public static async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return; // Double-check after lock
            
            // Create core services
            _vaultService = new VaultService();
            _vectorService = new SkillVectorService();
            _openApiParser = new OpenApiParser();
            _httpDispatcher = new HttpDispatcher(_vaultService);
            
            // Use Gemini if API key available, otherwise local fallback
            _embeddingService = !string.IsNullOrEmpty(_geminiApiKey)
                ? new GeminiEmbeddingService(_geminiApiKey)
                : new LocalEmbeddingService();
            
            // Initialize vector service (creates SQLite DB if needed)
            await _vectorService.InitializeAsync();
            
            // Create composite services
            _ingestionService = new IngestionService(_openApiParser, _embeddingService, _vectorService);
            _executorService = new ApiExecutorService(_vectorService, _httpDispatcher, _embeddingService, _vaultService);
            _toolHandler = new ApiSkillToolHandler(_executorService);
            
            _isInitialized = true;
            
            System.Diagnostics.Debug.WriteLine("[ApiOrchestration] Services initialized successfully");
        }
        finally
        {
            _initLock.Release();
        }
    }
    
    /// <summary>
    /// Initialize all services with the given API key.
    /// </summary>
    [Obsolete("Use Configure() + EnsureInitializedAsync() instead")]
    public static async Task InitializeAsync(string geminiApiKey)
    {
        Configure(geminiApiKey);
        await EnsureInitializedAsync();
    }
    
    /// <summary>
    /// Initialize with local embedding (no API key needed).
    /// </summary>
    [Obsolete("Use Configure() + EnsureInitializedAsync() instead")]
    public static async Task InitializeLocalAsync()
    {
        Configure(null);
        await EnsureInitializedAsync();
    }
    
    #region Service Accessors (Auto-Initialize)
    
    /// <summary>
    /// Get VaultService, auto-initializes if needed.
    /// </summary>
    public static IVaultService GetVaultService()
    {
        EnsureInitializedSync();
        return _vaultService!;
    }
    
    public static ISkillVectorService GetVectorService()
    {
        EnsureInitializedSync();
        return _vectorService!;
    }
    
    public static IEmbeddingService GetEmbeddingService()
    {
        EnsureInitializedSync();
        return _embeddingService!;
    }
    
    public static IOpenApiParser GetOpenApiParser()
    {
        EnsureInitializedSync();
        return _openApiParser!;
    }
    
    public static IHttpDispatcher GetHttpDispatcher()
    {
        EnsureInitializedSync();
        return _httpDispatcher!;
    }
    
    public static IIngestionService GetIngestionService()
    {
        EnsureInitializedSync();
        return _ingestionService!;
    }
    
    public static IApiExecutorService GetExecutorService()
    {
        EnsureInitializedSync();
        return _executorService!;
    }
    
    public static ApiSkillToolHandler GetToolHandler()
    {
        EnsureInitializedSync();
        return _toolHandler!;
    }
    
    #endregion
    
    /// <summary>
    /// Synchronous init wrapper for property accessors.
    /// Uses Task.Run to avoid deadlock on UI thread.
    /// </summary>
    private static void EnsureInitializedSync()
    {
        if (_isInitialized) return;
        
        // Run initialization async but wait synchronously
        // Safe for WPF because we use Task.Run
        Task.Run(async () => await EnsureInitializedAsync()).GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Check if services are initialized.
    /// </summary>
    public static bool IsInitialized => _isInitialized;
    
    /// <summary>
    /// Reset all services (for testing only).
    /// </summary>
    public static void Reset()
    {
        _initLock.Wait();
        try
        {
            (_vaultService as IDisposable)?.Dispose();
            (_vectorService as IDisposable)?.Dispose();
            
            _vaultService = null;
            _vectorService = null;
            _embeddingService = null;
            _openApiParser = null;
            _httpDispatcher = null;
            _ingestionService = null;
            _executorService = null;
            _toolHandler = null;
            _isInitialized = false;
        }
        finally
        {
            _initLock.Release();
        }
    }
}





