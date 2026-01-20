namespace WpfRagApp.Services.DataIngestion.Models;

/// <summary>
/// Result of AI semantic mapping between source columns and target parameters.
/// </summary>
public class MappingResult
{
    /// <summary>
    /// Column to parameter mappings.
    /// </summary>
    public List<ColumnMapping> Mappings { get; set; } = new();
    
    /// <summary>
    /// Default values for missing parameters.
    /// </summary>
    public List<DefaultValue> Defaults { get; set; } = new();
    
    /// <summary>
    /// Warnings about ambiguous or uncertain mappings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
    
    /// <summary>
    /// Overall confidence score (0.0 - 1.0).
    /// </summary>
    public double OverallConfidence => Mappings.Count > 0 
        ? Mappings.Average(m => m.Confidence) 
        : 0.0;
    
    /// <summary>
    /// True if any mapping has low confidence (< 0.7).
    /// </summary>
    public bool HasLowConfidenceMappings => Mappings.Any(m => m.Confidence < 0.7);
}

/// <summary>
/// Single column to parameter mapping.
/// </summary>
public class ColumnMapping
{
    /// <summary>
    /// Source column name from file.
    /// </summary>
    public string SourceColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// Target parameter name in method.
    /// </summary>
    public string TargetParameter { get; set; } = string.Empty;
    
    /// <summary>
    /// AI confidence score (0.0 - 1.0).
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Optional transformation (e.g., "ToUpper", "ParseDate").
    /// </summary>
    public string? Transformation { get; set; }
    
    /// <summary>
    /// True if user manually modified this mapping.
    /// </summary>
    public bool IsUserModified { get; set; }
}

/// <summary>
/// Default value for a missing parameter.
/// </summary>
public class DefaultValue
{
    /// <summary>
    /// Target parameter name.
    /// </summary>
    public string TargetParameter { get; set; } = string.Empty;
    
    /// <summary>
    /// Default value to use.
    /// </summary>
    public object? Value { get; set; }
    
    /// <summary>
    /// Reason for default (e.g., "Not found in source").
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}





