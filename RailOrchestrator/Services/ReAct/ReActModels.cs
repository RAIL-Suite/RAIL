namespace WpfRagApp.Services.ReAct;

/// <summary>
/// Represents a single step in a ReAct reasoning chain.
/// </summary>
public class ReActStep
{
    public int StepNumber { get; set; }
    public string Thought { get; set; } = string.Empty;
    public ReActAction Action { get; set; } = new();
    public string? Observation { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Represents an action in the ReAct format.
/// </summary>
public class ReActAction
{
    public ReActActionType Type { get; set; } = ReActActionType.Invalid;
    public string? FunctionName { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string? Answer { get; set; }
    public string RawText { get; set; } = string.Empty;
}

/// <summary>
/// Types of actions in ReAct format.
/// </summary>
public enum ReActActionType
{
    FunctionCall,
    Finish,
    Invalid
}

/// <summary>
/// Represents a complete ReAct session with all steps.
/// </summary>
public class ReActSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string UserQuery { get; set; } = string.Empty;
    public List<ReActStep> Steps { get; set; } = new();
    public string? FinalAnswer { get; set; }
    public ReActSessionStatus Status { get; set; } = ReActSessionStatus.InProgress;
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public int TotalTokensUsed { get; set; }

    public void AddStep(ReActStep step)
    {
        step.StepNumber = Steps.Count + 1;
        Steps.Add(step);
    }

    public TimeSpan? GetTotalDuration()
    {
        if (EndTime.HasValue)
            return EndTime.Value - StartTime;
        return null;
    }

    /// <summary>
    /// Format the session as a readable log.
    /// </summary>
    public string ToFormattedLog()
    {
        var sb = new System.Text.StringBuilder();
        
        foreach (var step in Steps)
        {
            sb.AppendLine($"--- Step {step.StepNumber} ---");
            sb.AppendLine($"💭 Thought: {step.Thought}");
            sb.AppendLine($"⚡ Action: {step.Action.RawText}");
            if (!string.IsNullOrEmpty(step.Observation))
                sb.AppendLine($"👁 Observation: {step.Observation}");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(FinalAnswer))
        {
            sb.AppendLine("=== Final Answer ===");
            sb.AppendLine(FinalAnswer);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Status of a ReAct session.
/// </summary>
public enum ReActSessionStatus
{
    InProgress,
    Completed,
    MaxStepsReached,
    Error,
    Cancelled
}

/// <summary>
/// Error types for function execution failures.
/// </summary>
public enum FunctionErrorType
{
    None,
    InvalidParameter,
    NotFound,
    PermissionDenied,
    ServiceUnavailable,
    Timeout,
    Unknown
}





