namespace WpfRagApp.Services.BulkExecution;

using System.Text.Json;
using RailFactory.Core;

/// <summary>
/// Handles bulk file processing with deterministic execution.
/// LLM plans once → Code loops without LLM.
/// </summary>
public class BulkExecutionService
{
    private readonly RailEngine _engine;
    
    public BulkExecutionService(RailEngine engine)
    {
        _engine = engine;
    }
    
    /// <summary>
    /// Execute a bulk operation plan returned by LLM.
    /// </summary>
    public async Task<BulkExecutionReport> ExecuteAsync(
        ExecutionPlan plan,
        IProgress<BulkProgress>? progress = null,
        CancellationToken ct = default)
    {
        var report = new BulkExecutionReport();
        var totalCalls = plan.Operations.Sum(op => op.UseBatch ? 1 : op.Calls.Count);
        var currentCall = 0;
        
        foreach (var operation in plan.Operations)
        {
            ct.ThrowIfCancellationRequested();
            
            if (operation.UseBatch && operation.BatchArgs != null)
            {
                // Single batch call
                currentCall++;
                progress?.Report(new BulkProgress(currentCall, totalCalls, operation.Function));
                
                var result = await ExecuteFunctionAsync(operation.Function, operation.BatchArgs);
                if (result.Success)
                    report.SuccessCount += operation.Calls.Count;
                else
                    report.Errors.Add(new BulkError(0, operation.Function, result.Error ?? "Unknown error"));
            }
            else
            {
                // Individual calls
                for (int i = 0; i < operation.Calls.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    currentCall++;
                    
                    progress?.Report(new BulkProgress(currentCall, totalCalls, operation.Function));
                    
                    var call = operation.Calls[i];
                    var result = await ExecuteFunctionAsync(operation.Function, call);
                    
                    if (result.Success)
                        report.SuccessCount++;
                    else
                        report.Errors.Add(new BulkError(i + 1, operation.Function, result.Error ?? "Unknown error"));
                }
            }
        }
        
        report.TotalCount = totalCalls;
        return report;
    }
    
    private async Task<FunctionResult> ExecuteFunctionAsync(string functionName, Dictionary<string, object> args)
    {
        try
        {
            var argsJson = JsonSerializer.Serialize(args);
            var responseJson = await Task.Run(() => _engine.Execute(functionName, argsJson));
            
            // Parse response
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("status", out var status) && status.GetString() == "error")
            {
                var error = root.TryGetProperty("error", out var e) ? e.GetString() : "Unknown error";
                return FunctionResult.Failure(error ?? "Unknown error");
            }
            
            return FunctionResult.Ok();
        }
        catch (Exception ex)
        {
            return FunctionResult.Failure(ex.Message);
        }
    }
}

#region Models

/// <summary>
/// Execution plan returned by LLM.
/// </summary>
public class ExecutionPlan
{
    public List<Operation> Operations { get; set; } = new();
    
    public static ExecutionPlan? TryParse(string json)
    {
        try
        {
            // Strip markdown code blocks if present
            var cleanJson = json.Trim();
            if (cleanJson.StartsWith("```"))
            {
                var lines = cleanJson.Split('\n');
                cleanJson = string.Join('\n', lines.Skip(1).TakeWhile(l => !l.StartsWith("```")));
            }
            
            var doc = JsonDocument.Parse(cleanJson);
            var plan = new ExecutionPlan();
            
            if (doc.RootElement.TryGetProperty("operations", out var ops))
            {
                foreach (var op in ops.EnumerateArray())
                {
                    var operation = new Operation
                    {
                        Function = op.GetProperty("function").GetString() ?? "",
                        UseBatch = op.TryGetProperty("useBatch", out var ub) && ub.GetBoolean()
                    };
                    
                    // Parse calls
                    if (op.TryGetProperty("calls", out var calls))
                    {
                        foreach (var call in calls.EnumerateArray())
                        {
                            var dict = new Dictionary<string, object>();
                            foreach (var prop in call.EnumerateObject())
                            {
                                dict[prop.Name] = GetJsonValue(prop.Value);
                            }
                            operation.Calls.Add(dict);
                        }
                    }
                    
                    // Parse batch args
                    if (op.TryGetProperty("args", out var args))
                    {
                        operation.BatchArgs = new Dictionary<string, object>();
                        foreach (var prop in args.EnumerateObject())
                        {
                            operation.BatchArgs[prop.Name] = GetJsonValue(prop.Value);
                        }
                    }
                    
                    plan.Operations.Add(operation);
                }
            }
            
            return plan.Operations.Count > 0 ? plan : null;
        }
        catch
        {
            return null;
        }
    }
    
    private static object GetJsonValue(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? "",
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => el.GetRawText()
        };
    }
}

public class Operation
{
    public string Function { get; set; } = "";
    public bool UseBatch { get; set; }
    public List<Dictionary<string, object>> Calls { get; set; } = new();
    public Dictionary<string, object>? BatchArgs { get; set; }
}

public class BulkExecutionReport
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public List<BulkError> Errors { get; set; } = new();
    
    public bool HasErrors => Errors.Count > 0;
    public int FailedCount => TotalCount - SuccessCount;
}

public record BulkError(int RowNumber, string Function, string Message);

public record BulkProgress(int Current, int Total, string CurrentFunction);

public record FunctionResult(bool Success, string? Error)
{
    public static FunctionResult Ok() => new(true, null);
    public static FunctionResult Failure(string error) => new(false, error);
}

#endregion





