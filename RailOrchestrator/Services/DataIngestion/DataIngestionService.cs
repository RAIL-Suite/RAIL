namespace WpfRagApp.Services.DataIngestion;

using RailFactory.Core;
using WpfRagApp.Services.DataIngestion.Execution;
using WpfRagApp.Services.DataIngestion.Interfaces;
using WpfRagApp.Services.DataIngestion.Mapping;
using WpfRagApp.Services.DataIngestion.Models;
using WpfRagApp.Services.DataIngestion.Routing;

/// <summary>
/// Main orchestrator for the data ingestion pipeline.
/// Coordinates: File Detection → Parsing → AI Mapping → Preview → Execution
/// </summary>
public class DataIngestionService
{
    private readonly IFileRouter _router;
    private readonly ISemanticMapper _mapper;
    private readonly Func<RailEngine> _engineFactory;
    
    // Current state (stateless after execution)
    private ParsedData? _currentParsedData;
    private MappingResult? _currentMapping;
    private MethodSignature? _currentTarget;
    
    public DataIngestionService(ILLMClient llmClient, Func<RailEngine> engineFactory)
    {
        _router = new FileTypeDetector();
        _mapper = new SemanticMapper(llmClient);
        _engineFactory = engineFactory;
    }
    
    /// <summary>
    /// Step 1: Parse dropped file and extract sample data.
    /// </summary>
    public async Task<ParsedData> ParseFileAsync(string filePath)
    {
        var fileType = _router.DetectType(filePath);
        
        if (fileType == FileType.Unknown)
            throw new NotSupportedException($"Unsupported file type: {Path.GetExtension(filePath)}");
        
        if (_router.RequiresAIExtraction(fileType))
            throw new NotSupportedException($"AI extraction for {fileType} not yet implemented");
        
        var parser = _router.GetParser(fileType);
        _currentParsedData = parser.ParseSample(filePath);
        
        return _currentParsedData;
    }
    
    /// <summary>
    /// Step 2: Use AI to map columns to target method.
    /// </summary>
    public async Task<MappingResult> MapToMethodAsync(
        MethodSignature targetMethod,
        CancellationToken ct = default)
    {
        if (_currentParsedData == null)
            throw new InvalidOperationException("No file parsed. Call ParseFileAsync first.");
        
        _currentTarget = targetMethod;
        _currentMapping = await _mapper.MapAsync(
            _currentParsedData.Headers,
            targetMethod,
            _currentParsedData.SampleRows,
            ct);
        
        return _currentMapping;
    }
    
    /// <summary>
    /// Step 3: Get preview data for UI display.
    /// </summary>
    public PreviewData GetPreviewData()
    {
        if (_currentParsedData == null || _currentMapping == null || _currentTarget == null)
            throw new InvalidOperationException("Mapping not complete. Call ParseFileAsync and MapToMethodAsync first.");
        
        return new PreviewData
        {
            SourceFile = _currentParsedData.SourceFile,
            TotalRows = _currentParsedData.TotalRowCount,
            Headers = _currentParsedData.Headers,
            SampleRows = _currentParsedData.SampleRows,
            Mapping = _currentMapping,
            TargetMethod = _currentTarget
        };
    }
    
    /// <summary>
    /// Step 4: Execute import with confirmed mapping.
    /// </summary>
    public async Task<ImportReport> ExecuteAsync(
        MappingResult confirmedMapping,
        ExecutionConfig? config = null,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (_currentParsedData == null || _currentTarget == null)
            throw new InvalidOperationException("No active import session.");
        
        config ??= ExecutionConfig.Default;
        
        // Get parser for streaming all rows
        var parser = _router.GetParser(_currentParsedData.FileType);
        var rows = parser.StreamRows(_currentParsedData.SourceFile);
        
        // Execute using RailEngine
        using var engine = _engineFactory();
        var executor = new DeterministicExecutor(engine);
        
        var report = await executor.ExecuteAsync(
            rows,
            confirmedMapping,
            _currentTarget,
            config,
            progress,
            ct);
        
        // Clear state after execution (stateless)
        ClearState();
        
        return report;
    }
    
    /// <summary>
    /// Update mapping manually (user corrections).
    /// </summary>
    public void UpdateMapping(MappingResult updatedMapping)
    {
        _currentMapping = updatedMapping;
    }
    
    /// <summary>
    /// Cancel current import session and clear state.
    /// </summary>
    public void CancelSession()
    {
        ClearState();
    }
    
    /// <summary>
    /// Check if an import session is active.
    /// </summary>
    public bool HasActiveSession => _currentParsedData != null;
    
    private void ClearState()
    {
        _currentParsedData = null;
        _currentMapping = null;
        _currentTarget = null;
    }
}

/// <summary>
/// Preview data for UI display.
/// </summary>
public class PreviewData
{
    public string SourceFile { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public string[] Headers { get; set; } = Array.Empty<string>();
    public Dictionary<string, object>[] SampleRows { get; set; } = Array.Empty<Dictionary<string, object>>();
    public MappingResult Mapping { get; set; } = new();
    public MethodSignature TargetMethod { get; set; } = new();
}





