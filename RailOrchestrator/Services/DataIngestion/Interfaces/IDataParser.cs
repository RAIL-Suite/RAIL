namespace WpfRagApp.Services.DataIngestion.Interfaces;

using WpfRagApp.Services.DataIngestion.Models;

/// <summary>
/// Parses structured files (Excel, CSV, JSON) extracting headers and data.
/// Designed for streaming to handle large files without memory issues.
/// </summary>
public interface IDataParser
{
    /// <summary>
    /// Supported file extensions for this parser.
    /// </summary>
    string[] SupportedExtensions { get; }
    
    /// <summary>
    /// Extract headers and sample rows for AI mapping.
    /// Never loads entire file into memory.
    /// </summary>
    /// <param name="filePath">Path to source file</param>
    /// <param name="sampleRows">Number of sample rows to extract (default 5)</param>
    /// <param name="sheetIndex">For Excel: sheet index (default 0 = first sheet)</param>
    ParsedData ParseSample(string filePath, int sampleRows = 5, int sheetIndex = 0);
    
    /// <summary>
    /// Stream all rows for execution using lazy enumeration.
    /// Memory efficient for large files.
    /// </summary>
    IEnumerable<Dictionary<string, object>> StreamRows(string filePath, int sheetIndex = 0);
    
    /// <summary>
    /// Get total row count without loading all data.
    /// </summary>
    int GetRowCount(string filePath, int sheetIndex = 0);
}





