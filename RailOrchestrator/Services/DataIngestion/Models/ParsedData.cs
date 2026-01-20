namespace WpfRagApp.Services.DataIngestion.Models;

/// <summary>
/// Represents parsed data from a source file - headers and sample rows only.
/// </summary>
public class ParsedData
{
    /// <summary>
    /// Column headers extracted from file.
    /// </summary>
    public string[] Headers { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Sample rows for AI analysis (typically 5).
    /// </summary>
    public Dictionary<string, object>[] SampleRows { get; set; } = Array.Empty<Dictionary<string, object>>();
    
    /// <summary>
    /// Estimated total row count (without loading all).
    /// </summary>
    public int TotalRowCount { get; set; }
    
    /// <summary>
    /// Original source file path.
    /// </summary>
    public string SourceFile { get; set; } = string.Empty;
    
    /// <summary>
    /// Detected file type.
    /// </summary>
    public Interfaces.FileType FileType { get; set; }
    
    /// <summary>
    /// Sheet name (for Excel files).
    /// </summary>
    public string? SheetName { get; set; }
}





