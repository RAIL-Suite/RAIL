namespace WpfRagApp.Services.DataIngestion.Interfaces;

using WpfRagApp.Services.DataIngestion.Models;

/// <summary>
/// Executes mapped data against RailEngine with throttling and error handling.
/// Deterministic loop - no AI involved.
/// </summary>
public interface IExecutionEngine
{
    /// <summary>
    /// Execute mapped data row by row against RailEngine.
    /// </summary>
    /// <param name="rows">Streamed rows from parser</param>
    /// <param name="mapping">Validated mapping result</param>
    /// <param name="targetMethod">Target method to invoke</param>
    /// <param name="config">Execution configuration (throttling, retries)</param>
    /// <param name="progress">Progress reporter for UI updates</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Import report with success/failure counts</returns>
    Task<ImportReport> ExecuteAsync(
        IEnumerable<Dictionary<string, object>> rows,
        MappingResult mapping,
        MethodSignature targetMethod,
        ExecutionConfig config,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Progress information for UI updates during execution.
/// </summary>
public class ExecutionProgress
{
    public int CurrentRow { get; set; }
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string? CurrentStatus { get; set; }
    
    public double PercentComplete => TotalRows > 0 ? (double)CurrentRow / TotalRows * 100 : 0;
}





