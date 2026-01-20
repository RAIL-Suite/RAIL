namespace WpfRagApp.Services.DataIngestion.Execution;

using RailFactory.Core;
using WpfRagApp.Services.DataIngestion.Interfaces;
using WpfRagApp.Services.DataIngestion.Models;
using System.Diagnostics;

/// <summary>
/// Deterministic executor for batch data import with throttling and error handling.
/// No AI involved - pure compiled code loop.
/// </summary>
public class DeterministicExecutor : IExecutionEngine
{
    private readonly RailEngine _engine;
    
    public DeterministicExecutor(RailEngine engine)
    {
        _engine = engine;
    }
    
    /// <inheritdoc/>
    public async Task<ImportReport> ExecuteAsync(
        IEnumerable<Dictionary<string, object>> rows,
        MappingResult mapping,
        MethodSignature targetMethod,
        ExecutionConfig config,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken ct = default)
    {
        var report = new ImportReport
        {
            Errors = new List<RowError>()
        };
        
        var stopwatch = Stopwatch.StartNew();
        var rowIndex = 0;
        var lastCallTime = DateTime.MinValue;
        var minInterval = TimeSpan.FromMilliseconds(1000.0 / config.MaxCallsPerSecond);
        
        // Materialize count for progress (if available)
        var rowList = rows as IList<Dictionary<string, object>> ?? rows.ToList();
        report.TotalRows = rowList.Count;
        
        foreach (var row in rowList)
        {
            ct.ThrowIfCancellationRequested();
            rowIndex++;
            
            try
            {
                // Throttling
                var elapsed = DateTime.Now - lastCallTime;
                if (elapsed < minInterval)
                {
                    await Task.Delay(minInterval - elapsed, ct);
                }
                
                // Transform row according to mapping
                var parameters = TransformRow(row, mapping);
                
                // Execute against RailEngine (sync wrapped in Task.Run)
                var methodCall = $"{targetMethod.ModuleName}.{targetMethod.MethodName}";
                var argsJson = Newtonsoft.Json.JsonConvert.SerializeObject(parameters);
                
                // Run sync Execute in background thread to not block UI
                var resultJson = await Task.Run(() => _engine.Execute(methodCall, argsJson), ct);
                lastCallTime = DateTime.Now;
                
                // Parse result - success if no exception
                report.SuccessCount++;
            }
            catch (Exception ex)
            {
                HandleError(report, rowIndex, ex.Message, row, config);
                
                if (!config.ContinueOnError)
                    throw;
            }
            
            // Report progress
            if (progress != null && rowIndex % config.BatchSize == 0)
            {
                progress.Report(new ExecutionProgress
                {
                    CurrentRow = rowIndex,
                    TotalRows = report.TotalRows,
                    SuccessCount = report.SuccessCount,
                    FailedCount = report.FailedCount,
                    CurrentStatus = $"Processing row {rowIndex}/{report.TotalRows}"
                });
            }
        }
        
        stopwatch.Stop();
        report.Duration = stopwatch.Elapsed;
        
        // Final progress report
        progress?.Report(new ExecutionProgress
        {
            CurrentRow = report.TotalRows,
            TotalRows = report.TotalRows,
            SuccessCount = report.SuccessCount,
            FailedCount = report.FailedCount,
            CurrentStatus = report.GetSummary()
        });
        
        return report;
    }
    
    /// <summary>
    /// Transform source row to target parameters using mapping.
    /// </summary>
    private static Dictionary<string, object?> TransformRow(
        Dictionary<string, object> sourceRow,
        MappingResult mapping)
    {
        var result = new Dictionary<string, object?>();
        
        // Apply column mappings
        foreach (var m in mapping.Mappings)
        {
            if (sourceRow.TryGetValue(m.SourceColumn, out var value))
            {
                result[m.TargetParameter] = ApplyTransformation(value, m.Transformation);
            }
        }
        
        // Apply defaults for missing parameters
        foreach (var d in mapping.Defaults)
        {
            if (!result.ContainsKey(d.TargetParameter))
            {
                result[d.TargetParameter] = d.Value;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Apply optional transformation to value.
    /// </summary>
    private static object? ApplyTransformation(object value, string? transformation)
    {
        if (string.IsNullOrEmpty(transformation))
            return value;
        
        return transformation.ToLowerInvariant() switch
        {
            "toupper" => value?.ToString()?.ToUpperInvariant(),
            "tolower" => value?.ToString()?.ToLowerInvariant(),
            "trim" => value?.ToString()?.Trim(),
            "parseint" => int.TryParse(value?.ToString(), out var i) ? i : value,
            "parsedouble" => double.TryParse(value?.ToString(), out var d) ? d : value,
            _ => value
        };
    }
    
    /// <summary>
    /// Handle row error with logging.
    /// </summary>
    private static void HandleError(
        ImportReport report,
        int rowIndex,
        string errorMessage,
        Dictionary<string, object> rowData,
        ExecutionConfig config)
    {
        report.FailedCount++;
        report.Errors.Add(new RowError
        {
            RowIndex = rowIndex,
            ErrorMessage = errorMessage,
            RowData = rowData
        });
    }
}





