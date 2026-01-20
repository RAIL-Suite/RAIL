namespace RailFactory.Core.Events;

/// <summary>
/// Event raised when a function is called via IPC.
/// Used for UI highlighting and workflow visualization.
/// </summary>
public class FunctionCallEvent
{
    /// <summary>
    /// Name of the function being called.
    /// </summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// Parameters passed to the function.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>
    /// Timestamp of the event.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Phase of execution: "before" or "after".
    /// </summary>
    public string Phase { get; set; } = "before";

    /// <summary>
    /// Result of the function call (only populated for "after" phase).
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Command sent to host application for UI highlighting.
/// </summary>
public class UIHighlightCommand
{
    public string CommandType { get; set; } = "UI_HIGHLIGHT";
    public string FunctionName { get; set; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
}



