// ============================================================================
// JSON SERIALIZATION CONTEXT - AOT COMPATIBLE
// ============================================================================
// Source-generated JSON serialization for Native AOT compatibility.
// Eliminates reflection-based serialization that causes IL3050 warnings.
//
// ============================================================================

using System.Text.Json.Serialization;

namespace RailBridge.Native;

/// <summary>
/// Source-generated JSON serialization context for AOT compatibility.
/// </summary>
[JsonSerializable(typeof(ProtocolMessage))]
[JsonSerializable(typeof(ConnectMessage))]
[JsonSerializable(typeof(ResultMessage))]
[JsonSerializable(typeof(ExecuteCommand))]
[JsonSerializable(typeof(CallbackCommand))]
[JsonSerializable(typeof(CallbackResult))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class BridgeJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Command sent to SDK callback.
/// </summary>
internal class CallbackCommand
{
    public string Method { get; set; } = "";
    public Dictionary<string, object?>? Args { get; set; }
}

/// <summary>
/// Result from SDK callback.
/// </summary>
internal class CallbackResult
{
    public string Status { get; set; } = "";
    public object? Result { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Execute command from Host.
/// </summary>
internal class ExecuteCommand
{
    public string Type { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string Method { get; set; } = "";
    public Dictionary<string, object?>? Args { get; set; }
}



