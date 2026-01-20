using System.Text;

namespace WpfRagApp.Services.DataIngestion.Parsing;

using WpfRagApp.Services.DataIngestion.Interfaces;
using WpfRagApp.Services.DataIngestion.Models;

/// <summary>
/// Parser for CSV files with streaming support.
/// </summary>
public class CsvParser : IDataParser
{
    private readonly char[] _possibleDelimiters = { ',', ';', '\t', '|' };
    
    /// <inheritdoc/>
    public string[] SupportedExtensions => new[] { ".csv" };
    
    /// <inheritdoc/>
    public ParsedData ParseSample(string filePath, int sampleRows = 5, int sheetIndex = 0)
    {
        var lines = File.ReadLines(filePath).Take(sampleRows + 1).ToList();
        
        if (lines.Count == 0)
        {
            return new ParsedData
            {
                SourceFile = filePath,
                FileType = FileType.Csv,
                Headers = Array.Empty<string>(),
                SampleRows = Array.Empty<Dictionary<string, object>>(),
                TotalRowCount = 0
            };
        }
        
        var delimiter = DetectDelimiter(lines[0]);
        var headers = ParseLine(lines[0], delimiter);
        
        var dataRows = lines
            .Skip(1)
            .Select(line => BuildRowDictionary(ParseLine(line, delimiter), headers))
            .ToArray();
        
        // Count total lines (lazy)
        var totalRows = File.ReadLines(filePath).Count() - 1;
        
        return new ParsedData
        {
            SourceFile = filePath,
            FileType = FileType.Csv,
            Headers = headers,
            SampleRows = dataRows,
            TotalRowCount = totalRows
        };
    }
    
    /// <inheritdoc/>
    public IEnumerable<Dictionary<string, object>> StreamRows(string filePath, int sheetIndex = 0)
    {
        using var reader = new StreamReader(filePath);
        
        var headerLine = reader.ReadLine();
        if (headerLine == null)
            yield break;
        
        var delimiter = DetectDelimiter(headerLine);
        var headers = ParseLine(headerLine, delimiter);
        
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
            yield return BuildRowDictionary(ParseLine(line, delimiter), headers);
        }
    }
    
    /// <inheritdoc/>
    public int GetRowCount(string filePath, int sheetIndex = 0)
    {
        return File.ReadLines(filePath).Count() - 1; // Exclude header
    }
    
    private char DetectDelimiter(string line)
    {
        var counts = _possibleDelimiters
            .Select(d => new { Delimiter = d, Count = line.Count(c => c == d) })
            .OrderByDescending(x => x.Count)
            .ToList();
        
        return counts.FirstOrDefault()?.Delimiter ?? ',';
    }
    
    private static string[] ParseLine(string line, char delimiter)
    {
        // Simple CSV parsing - handles basic cases
        // For complex CSVs with quotes, use CsvHelper
        var result = new List<string>();
        var inQuotes = false;
        var current = new System.Text.StringBuilder();
        
        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim());
        
        return result.ToArray();
    }
    
    private static Dictionary<string, object> BuildRowDictionary(string[] values, string[] headers)
    {
        var dict = new Dictionary<string, object>();
        
        for (int i = 0; i < headers.Length && i < values.Length; i++)
        {
            dict[headers[i]] = values[i];
        }
        
        return dict;
    }
}





