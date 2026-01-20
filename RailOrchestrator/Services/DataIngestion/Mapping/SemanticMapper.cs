namespace WpfRagApp.Services.DataIngestion.Mapping;

using WpfRagApp.Services.DataIngestion.Interfaces;
using WpfRagApp.Services.DataIngestion.Models;
using System.Text.Json;

/// <summary>
/// AI-powered semantic mapper using LLM to correlate source columns to target parameters.
/// </summary>
public class SemanticMapper : ISemanticMapper
{
    private readonly ILLMClient _llmClient;
    
    public SemanticMapper(ILLMClient llmClient)
    {
        _llmClient = llmClient;
    }
    
    /// <inheritdoc/>
    public async Task<MappingResult> MapAsync(
        string[] sourceHeaders,
        MethodSignature targetMethod,
        Dictionary<string, object>[] sampleRows,
        CancellationToken ct = default)
    {
        var prompt = BuildMappingPrompt(sourceHeaders, targetMethod, sampleRows);
        var response = await _llmClient.GenerateAsync(prompt, ct);
        
        return ParseMappingResponse(response);
    }
    
    /// <inheritdoc/>
    public bool ValidateMapping(MappingResult mapping, MethodSignature targetMethod)
    {
        // Check all required parameters are mapped
        var requiredParams = targetMethod.Parameters
            .Where(p => p.IsRequired)
            .Select(p => p.Name)
            .ToHashSet();
        
        var mappedParams = mapping.Mappings
            .Select(m => m.TargetParameter)
            .ToHashSet();
        
        var defaultParams = mapping.Defaults
            .Select(d => d.TargetParameter)
            .ToHashSet();
        
        var covered = mappedParams.Union(defaultParams);
        return requiredParams.IsSubsetOf(covered);
    }
    
    private static string BuildMappingPrompt(
        string[] sourceHeaders,
        MethodSignature targetMethod,
        Dictionary<string, object>[] sampleRows)
    {
        var headersJson = JsonSerializer.Serialize(sourceHeaders);
        var sampleJson = JsonSerializer.Serialize(sampleRows.Take(3));
        var paramsJson = JsonSerializer.Serialize(targetMethod.Parameters.Select(p => new
        {
            p.Name,
            p.Type,
            p.IsRequired,
            p.Description
        }));
        
        var sb = new StringBuilder();
        sb.AppendLine("You are a data mapping expert. Map source columns to target method parameters.");
        sb.AppendLine();
        sb.AppendLine("SOURCE HEADERS:");
        sb.AppendLine(headersJson);
        sb.AppendLine();
        sb.AppendLine("SAMPLE DATA (first 3 rows):");
        sb.AppendLine(sampleJson);
        sb.AppendLine();
        sb.AppendLine($"TARGET METHOD: {targetMethod.ModuleName}.{targetMethod.MethodName}");
        sb.AppendLine("PARAMETERS:");
        sb.AppendLine(paramsJson);
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Match source columns to target parameters by semantic meaning");
        sb.AppendLine("2. Assign confidence 0.0-1.0 for each mapping");
        sb.AppendLine("3. If a required parameter has no match, add to 'defaults' with null value");
        sb.AppendLine("4. Flag ambiguous matches (confidence < 0.7) in warnings");
        sb.AppendLine();
        sb.AppendLine("OUTPUT JSON FORMAT:");
        sb.AppendLine("{");
        sb.AppendLine("  \"mappings\": [");
        sb.AppendLine("    { \"source\": \"Column A\", \"target\": \"ParameterName\", \"confidence\": 0.95 }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"defaults\": [");
        sb.AppendLine("    { \"target\": \"MissingParam\", \"value\": null, \"reason\": \"Not found in source\" }");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"warnings\": [\"Ambiguous: 'Date1' and 'Date2' both match 'Date'\"]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("RESPOND WITH ONLY VALID JSON, NO MARKDOWN.");
        
        return sb.ToString();
    }
    
    private static MappingResult ParseMappingResponse(string response)
    {
        try
        {
            // Clean response (remove markdown if present)
            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).TakeWhile(l => !l.StartsWith("```")));
            }
            
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var result = new MappingResult();
            
            // Parse mappings
            if (root.TryGetProperty("mappings", out var mappingsEl))
            {
                foreach (var m in mappingsEl.EnumerateArray())
                {
                    result.Mappings.Add(new ColumnMapping
                    {
                        SourceColumn = m.GetProperty("source").GetString() ?? "",
                        TargetParameter = m.GetProperty("target").GetString() ?? "",
                        Confidence = m.GetProperty("confidence").GetDouble()
                    });
                }
            }
            
            // Parse defaults
            if (root.TryGetProperty("defaults", out var defaultsEl))
            {
                foreach (var d in defaultsEl.EnumerateArray())
                {
                    result.Defaults.Add(new DefaultValue
                    {
                        TargetParameter = d.GetProperty("target").GetString() ?? "",
                        Value = d.TryGetProperty("value", out var v) ? GetJsonValue(v) : null,
                        Reason = d.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : ""
                    });
                }
            }
            
            // Parse warnings
            if (root.TryGetProperty("warnings", out var warningsEl))
            {
                foreach (var w in warningsEl.EnumerateArray())
                {
                    result.Warnings.Add(w.GetString() ?? "");
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return new MappingResult
            {
                Warnings = new List<string> { $"Failed to parse AI response: {ex.Message}" }
            };
        }
    }
    
    private static object? GetJsonValue(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.ToString()
        };
    }
}

/// <summary>
/// LLM client interface for generating responses.
/// </summary>
public interface ILLMClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
}





