namespace WpfRagApp.Services.DataIngestion.Models;

/// <summary>
/// Configuration for batch execution with throttling and error handling.
/// </summary>
public class ExecutionConfig
{
    /// <summary>
    /// Maximum calls per second to prevent rate limiting.
    /// </summary>
    public int MaxCallsPerSecond { get; set; } = 10;
    
    /// <summary>
    /// Delay between calls in milliseconds.
    /// </summary>
    public int DelayBetweenCallsMs { get; set; } = 100;
    
    /// <summary>
    /// Number of rows to process before yielding to UI.
    /// </summary>
    public int BatchSize { get; set; } = 50;
    
    /// <summary>
    /// Retry on rate limit errors.
    /// </summary>
    public bool RetryOnRateLimit { get; set; } = true;
    
    /// <summary>
    /// Maximum retry attempts per row.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Continue processing on individual row errors.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;
    
    /// <summary>
    /// Default configuration for typical use.
    /// </summary>
    public static ExecutionConfig Default => new();
    
    /// <summary>
    /// Fast configuration (no throttling) - use with caution.
    /// </summary>
    public static ExecutionConfig Fast => new()
    {
        MaxCallsPerSecond = 100,
        DelayBetweenCallsMs = 10,
        BatchSize = 100
    };
    
    /// <summary>
    /// Safe configuration for rate-limited systems.
    /// </summary>
    public static ExecutionConfig Safe => new()
    {
        MaxCallsPerSecond = 5,
        DelayBetweenCallsMs = 200,
        BatchSize = 25,
        MaxRetries = 5
    };
}





