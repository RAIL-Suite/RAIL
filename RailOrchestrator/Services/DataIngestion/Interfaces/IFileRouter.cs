namespace WpfRagApp.Services.DataIngestion.Interfaces;

/// <summary>
/// Routes incoming files to appropriate parsers based on file type detection.
/// </summary>
public interface IFileRouter
{
    /// <summary>
    /// Detects file type using magic bytes and extension validation.
    /// </summary>
    FileType DetectType(string filePath);
    
    /// <summary>
    /// Gets the appropriate parser for the detected file type.
    /// </summary>
    IDataParser GetParser(FileType type);
    
    /// <summary>
    /// Checks if the file type requires AI for extraction (PDF, DOCX, etc.).
    /// </summary>
    bool RequiresAIExtraction(FileType type);
}

/// <summary>
/// Supported file types for data ingestion.
/// </summary>
public enum FileType
{
    Unknown,
    Excel,      // .xlsx, .xls
    Csv,        // .csv
    Json,       // .json
    Pdf,        // .pdf - requires AI
    Word,       // .docx - requires AI
    Email       // .eml, .msg - requires AI
}





