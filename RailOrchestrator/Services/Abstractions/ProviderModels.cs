using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace WpfRagApp.Services.Abstractions
{
    /// <summary>
    /// Represents a message in the conversation history, agnostic of the specific provider.
    /// </summary>
    public class ProviderMessage
    {
        public string Role { get; set; } = "user"; // "user", "model" (or "assistant"), "system", "tool"
        public string? Content { get; set; }
        public string? ToolCallId { get; set; } // For providers that require linking tool responses to calls (OpenAI/Anthropic)
        
        // Optional: Artifacts like images or audio
        public byte[]? AudioData { get; set; }
        public string? MimeType { get; set; }
    }

    /// <summary>
    /// Standardized response from an LLM provider.
    /// </summary>
    public class ProviderResponse
    {
        public string? TextContent { get; set; }
        public List<ProviderFunctionCall> FunctionCalls { get; set; } = new();
        public string? FinishReason { get; set; }
        public object? OriginalResponse { get; set; } // Access to the raw provider response if needed
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

    /// <summary>
    /// Represents a request to execute a tool/function.
    /// </summary>
    public class ProviderFunctionCall
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Unique ID for this call
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object?> Arguments { get; set; } = new();
    }

    /// <summary>
    /// Configuration for the request (temperature, etc.)
    /// </summary>
    public class ProviderConfig
    {
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 2048;
        public List<string>? StopSequences { get; set; }
        public string ModelId { get; set; } = string.Empty;
        public bool JsonMode { get; set; } = false;
    }
}
