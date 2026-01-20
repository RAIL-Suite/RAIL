namespace WpfRagApp.Services.DataIngestion.Parsing;

using ClosedXML.Excel;
using WpfRagApp.Services.DataIngestion.Interfaces;
using WpfRagApp.Services.DataIngestion.Models;

/// <summary>
/// Parser for Excel files (.xlsx, .xls) using ClosedXML.
/// Streams rows to minimize memory usage.
/// </summary>
public class ExcelParser : IDataParser
{
    /// <inheritdoc/>
    public string[] SupportedExtensions => new[] { ".xlsx", ".xls" };
    
    /// <inheritdoc/>
    public ParsedData ParseSample(string filePath, int sampleRows = 5, int sheetIndex = 0)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.Skip(sheetIndex).FirstOrDefault()
            ?? throw new InvalidOperationException($"Sheet index {sheetIndex} not found");
        
        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
        {
            return new ParsedData
            {
                SourceFile = filePath,
                FileType = FileType.Excel,
                SheetName = worksheet.Name,
                Headers = Array.Empty<string>(),
                SampleRows = Array.Empty<Dictionary<string, object>>(),
                TotalRowCount = 0
            };
        }
        
        var firstRow = usedRange.FirstRow();
        var headers = firstRow.Cells()
            .Select(c => c.GetString().Trim())
            .Where(h => !string.IsNullOrEmpty(h))
            .ToArray();
        
        var dataRows = usedRange.RowsUsed()
            .Skip(1) // Skip header
            .Take(sampleRows)
            .Select(row => BuildRowDictionary(row, headers))
            .ToArray();
        
        var totalRows = usedRange.RowCount() - 1; // Exclude header
        
        return new ParsedData
        {
            SourceFile = filePath,
            FileType = FileType.Excel,
            SheetName = worksheet.Name,
            Headers = headers,
            SampleRows = dataRows,
            TotalRowCount = totalRows
        };
    }
    
    /// <inheritdoc/>
    public IEnumerable<Dictionary<string, object>> StreamRows(string filePath, int sheetIndex = 0)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.Skip(sheetIndex).FirstOrDefault();
        
        if (worksheet == null)
            yield break;
        
        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
            yield break;
        
        var firstRow = usedRange.FirstRow();
        var headers = firstRow.Cells()
            .Select(c => c.GetString().Trim())
            .Where(h => !string.IsNullOrEmpty(h))
            .ToArray();
        
        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            yield return BuildRowDictionary(row, headers);
        }
    }
    
    /// <inheritdoc/>
    public int GetRowCount(string filePath, int sheetIndex = 0)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.Skip(sheetIndex).FirstOrDefault();
        
        if (worksheet == null)
            return 0;
        
        var usedRange = worksheet.RangeUsed();
        return usedRange?.RowCount() - 1 ?? 0; // Exclude header
    }
    
    private static Dictionary<string, object> BuildRowDictionary(IXLRangeRow row, string[] headers)
    {
        var dict = new Dictionary<string, object>();
        var cells = row.Cells().ToArray();
        
        for (int i = 0; i < headers.Length && i < cells.Length; i++)
        {
            var cell = cells[i];
            object value = cell.DataType switch
            {
                XLDataType.Number => cell.GetDouble(),
                XLDataType.DateTime => cell.GetDateTime(),
                XLDataType.Boolean => cell.GetBoolean(),
                _ => cell.GetString()
            };
            dict[headers[i]] = value;
        }
        
        return dict;
    }
}





