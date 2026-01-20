namespace WpfRagApp.Services.DataIngestion.Models;

/// <summary>
/// Final report after batch import execution.
/// </summary>
public class ImportReport
{
    /// <summary>
    /// Total rows attempted.
    /// </summary>
    public int TotalRows { get; set; }
    
    /// <summary>
    /// Successfully processed rows.
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Failed rows (logged in Errors).
    /// </summary>
    public int FailedCount { get; set; }
    
    /// <summary>
    /// Skipped rows (invalid data).
    /// </summary>
    public int SkippedCount { get; set; }
    
    /// <summary>
    /// Total execution duration.
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Detailed error list.
    /// </summary>
    public List<RowError> Errors { get; set; } = new();
    
    /// <summary>
    /// True if all rows succeeded.
    /// </summary>
    public bool IsFullSuccess => FailedCount == 0 && SkippedCount == 0;
    
    /// <summary>
    /// Success rate percentage.
    /// </summary>
    public double SuccessRate => TotalRows > 0 
        ? (double)SuccessCount / TotalRows * 100 
        : 0;
    
    /// <summary>
    /// Generate summary message for UI.
    /// </summary>
    public string GetSummary()
    {
        if (IsFullSuccess)
            return $"✅ Import completed: {SuccessCount} rows in {Duration.TotalSeconds:F1}s";
        
        return $"⚠️ Import completed: {SuccessCount}/{TotalRows} rows ({SuccessRate:F0}%), {FailedCount} errors";
    }
}

/// <summary>
/// Error detail for a single row.
/// </summary>
public class RowError
{
    /// <summary>
    /// Row index (1-based for user display).
    /// </summary>
    public int RowIndex { get; set; }
    
    /// <summary>
    /// Error message from execution.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Original row data for debugging.
    /// </summary>
    public Dictionary<string, object>? RowData { get; set; }
    
    /// <summary>
    /// Exception type if available.
    /// </summary>
    public string? ExceptionType { get; set; }
}





