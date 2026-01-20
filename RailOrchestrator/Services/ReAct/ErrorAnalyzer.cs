namespace WpfRagApp.Services.ReAct;

/// <summary>
/// Analyzes function execution errors and provides correction hints.
/// </summary>
public class ErrorAnalyzer
{
    /// <summary>
    /// Classify an error message into a known error type.
    /// </summary>
    public FunctionErrorType Classify(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return FunctionErrorType.None;

        var lower = errorMessage.ToLowerInvariant();

        // Invalid parameter patterns
        if (lower.Contains("invalid") || 
            lower.Contains("must be") ||
            lower.Contains("expected") ||
            lower.Contains("use:") ||
            lower.Contains("should be"))
        {
            return FunctionErrorType.InvalidParameter;
        }

        // Not found patterns
        if (lower.Contains("not found") || 
            lower.Contains("does not exist") ||
            lower.Contains("unknown") ||
            lower.Contains("no such"))
        {
            return FunctionErrorType.NotFound;
        }

        // Permission patterns
        if (lower.Contains("permission") || 
            lower.Contains("access denied") ||
            lower.Contains("unauthorized") ||
            lower.Contains("forbidden"))
        {
            return FunctionErrorType.PermissionDenied;
        }

        // Service unavailable patterns
        if (lower.Contains("unavailable") || 
            lower.Contains("connection") ||
            lower.Contains("service error") ||
            lower.Contains("cannot connect"))
        {
            return FunctionErrorType.ServiceUnavailable;
        }

        // Timeout patterns
        if (lower.Contains("timeout") || 
            lower.Contains("timed out"))
        {
            return FunctionErrorType.Timeout;
        }

        return FunctionErrorType.Unknown;
    }

    /// <summary>
    /// Generate a correction hint for the LLM based on error type.
    /// </summary>
    public string GenerateCorrectionHint(FunctionErrorType errorType, string errorMessage)
    {
        return errorType switch
        {
            FunctionErrorType.InvalidParameter =>
                $"⚠️ Parameter Error: {errorMessage}\n" +
                "Hint: Check the function description for valid parameter values. " +
                "String parameters may need specific values (e.g., enum-like constraints).",

            FunctionErrorType.NotFound =>
                $"⚠️ Not Found: {errorMessage}\n" +
                "Hint: The referenced entity doesn't exist. " +
                "Try calling a query function first to get valid IDs/codes.",

            FunctionErrorType.PermissionDenied =>
                $"⚠️ Access Denied: {errorMessage}\n" +
                "Hint: This action may not be available. Consider an alternative approach.",

            FunctionErrorType.ServiceUnavailable =>
                $"⚠️ Service Error: {errorMessage}\n" +
                "Hint: The target service is unavailable. You may retry or skip this step.",

            FunctionErrorType.Timeout =>
                $"⚠️ Timeout: {errorMessage}\n" +
                "Hint: The operation took too long. Consider simplifying the request.",

            _ =>
                $"⚠️ Error: {errorMessage}\n" +
                "Hint: Analyze this error and adjust your approach."
        };
    }

    /// <summary>
    /// Check if an error message indicates a failure that should trigger retry.
    /// </summary>
    public bool ShouldRetry(string result)
    {
        if (string.IsNullOrEmpty(result))
            return false;

        var lower = result.ToLowerInvariant();
        
        // Error patterns that indicate a retriable failure
        return lower.Contains("error") ||
               lower.Contains("invalid") ||
               lower.Contains("failed") ||
               lower.Contains("not found") ||
               lower.Contains("exception");
    }
}





