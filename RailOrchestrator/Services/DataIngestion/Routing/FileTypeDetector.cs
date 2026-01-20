namespace WpfRagApp.Services.DataIngestion.Routing;

using WpfRagApp.Services.DataIngestion.Interfaces;
using WpfRagApp.Services.DataIngestion.Parsing;

/// <summary>
/// Routes files to appropriate parsers based on magic bytes and extension.
/// </summary>
public class FileTypeDetector : IFileRouter
{
    private readonly Dictionary<FileType, IDataParser> _parsers = new();
    
    public FileTypeDetector()
    {
        // Register parsers
        _parsers[FileType.Excel] = new ExcelParser();
        _parsers[FileType.Csv] = new CsvParser();
        // PDF and others would use AI extraction - not IDataParser
    }
    
    /// <inheritdoc/>
    public FileType DetectType(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return FileType.Unknown;
        
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Check magic bytes + extension for security
        return extension switch
        {
            ".xlsx" when MagicBytes.IsOfficeOpenXml(filePath) => FileType.Excel,
            ".xls" when MagicBytes.MatchesSignature(filePath, MagicBytes.XLS) => FileType.Excel,
            ".csv" => FileType.Csv, // Text file - no magic bytes
            ".json" => FileType.Json,
            ".pdf" when MagicBytes.MatchesSignature(filePath, MagicBytes.PDF) => FileType.Pdf,
            ".docx" when MagicBytes.IsOfficeOpenXml(filePath) => FileType.Word,
            ".eml" or ".msg" => FileType.Email,
            _ => FileType.Unknown
        };
    }
    
    /// <inheritdoc/>
    public IDataParser GetParser(FileType type)
    {
        if (_parsers.TryGetValue(type, out var parser))
            return parser;
        
        throw new NotSupportedException($"No parser available for file type: {type}");
    }
    
    /// <inheritdoc/>
    public bool RequiresAIExtraction(FileType type)
    {
        return type switch
        {
            FileType.Pdf => true,
            FileType.Word => true,
            FileType.Email => true,
            _ => false
        };
    }
}





