namespace WpfRagApp.Services.DataIngestion.Interfaces;

using WpfRagApp.Services.DataIngestion.Models;

/// <summary>
/// Uses AI to semantically map source columns to target method parameters.
/// </summary>
public interface ISemanticMapper
{
    /// <summary>
    /// Maps source file columns to target method parameters using AI inference.
    /// </summary>
    /// <param name="sourceHeaders">Column headers from source file</param>
    /// <param name="targetMethod">Method signature with parameter info</param>
    /// <param name="sampleRows">Sample data rows for context</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Mapping result with confidence scores</returns>
    Task<MappingResult> MapAsync(
        string[] sourceHeaders,
        MethodSignature targetMethod,
        Dictionary<string, object>[] sampleRows,
        CancellationToken ct = default);
    
    /// <summary>
    /// Validates a user-modified mapping.
    /// </summary>
    bool ValidateMapping(MappingResult mapping, MethodSignature targetMethod);
}

/// <summary>
/// Represents a target method signature for mapping.
/// </summary>
public class MethodSignature
{
    public string ModuleName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = new();
}

/// <summary>
/// Parameter information for mapping.
/// </summary>
public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public string? Description { get; set; }
}





