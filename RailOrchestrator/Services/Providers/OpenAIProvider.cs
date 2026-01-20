using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfRagApp.Models;
using WpfRagApp.Services.Abstractions;

namespace WpfRagApp.Services.Providers
{
    public class OpenAIProvider : ILLMProvider
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        public string ProviderId => "openai";
        public bool SupportsAudioInput => false; // OpenAI Chat API doesn't support direct audio bytes yet (uses Whisper API separately)

        public OpenAIProvider(string apiKey, HttpClient? httpClient = null)
        {
            _apiKey = apiKey;
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<ProviderResponse> ChatAsync(
            List<ProviderMessage> history, 
            string? toolsJson = null, 
            ProviderConfig? config = null, 
            CancellationToken ct = default)
        {
            var modelId = config?.ModelId ?? "gpt-4o";
            var temperature = config?.Temperature ?? 0.7;

            // 1. Prepare Messages
            var messages = history.Select(m => MapMessage(m)).ToList();

            // 2. Prepare Tools (Adapter)
            object? tools = null;
            if (!string.IsNullOrWhiteSpace(toolsJson) && toolsJson != "[]")
            {
                tools = AdaptTools(toolsJson);
            }

            // 3. Build Request
            var requestBody = new
            {
                model = modelId,
                messages = messages,
                tools = tools,
                temperature = temperature
            };

            // 4. Send Request
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            var jsonContent = JsonSerializer.Serialize(requestBody, jsonOptions);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new Exception($"OpenAI API Error: {response.StatusCode} - {error}");
            }

            var resultJson = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(resultJson);
        }

        public Task<string> TranscribeAudioAsync(byte[] audioData, CancellationToken ct = default)
        {
            // Would implement Whisper API here
            throw new NotImplementedException("Audio transcription not yet implemented for OpenAI provider.");
        }

        // --- Adapters ---

        private object MapMessage(ProviderMessage msg)
        {
            if (msg.Role == "tool")
            {
                return new
                {
                    role = "tool",
                    tool_call_id = msg.ToolCallId,
                    content = msg.Content
                };
            }
            
            return new
            {
                role = msg.Role == "model" ? "assistant" : msg.Role,
                content = msg.Content,
                tool_calls = (object?)null // We don't send tool_calls back in history unless it's an assistant message that MADE the call
            };

            // Note: Handling "Assistant message with tool calls" requires storing the original tool_calls in history.
            // For now, simpler mapping. Proper implementation separates content and tool_calls.
        }

        /// <summary>
        /// Adapts RAIL tools (flat list) to OpenAI tools format.
        /// RAIL: [ { "name": "...", "parameters": ... } ]
        /// OpenAI: [ { "type": "function", "function": { "name": "...", "parameters": ... } } ]
        /// </summary>
        private List<object>? AdaptTools(string toolsJson)
        {
            try
            {
                var node = JsonNode.Parse(toolsJson);
                if (node is not JsonArray arr) return null;

                var openAiTools = new List<object>();

                foreach (var tool in arr)
                {
                    if (tool is JsonObject toolObj)
                    {
                        // Clean up RAIL specific fields if needed
                        toolObj.Remove("class"); 
                        
                        openAiTools.Add(new
                        {
                            type = "function",
                            function = toolObj // Pass the whole object as the function definition
                        });
                    }
                }

                return openAiTools.Any() ? openAiTools : null;
            }
            catch
            {
                return null;
            }
        }

        private ProviderResponse ParseResponse(string json)
        {
            var response = new ProviderResponse();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var choice = root.GetProperty("choices")[0];
            var message = choice.GetProperty("message");

            // Text content
            if (message.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
            {
                response.TextContent = contentProp.GetString();
            }

            // Tool calls
            if (message.TryGetProperty("tool_calls", out var toolCallsProp))
            {
                foreach (var toolCall in toolCallsProp.EnumerateArray())
                {
                    var function = toolCall.GetProperty("function");
                    var name = function.GetProperty("name").GetString() ?? "";
                    var argsJson = function.GetProperty("arguments").GetString() ?? "{}";
                    
                    var argsDict = new Dictionary<string, object?>();
                    try { argsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson); } catch {}

                    response.FunctionCalls.Add(new ProviderFunctionCall
                    {
                        Id = toolCall.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                        Name = name,
                        Arguments = argsDict!
                    });
                }
            }

            // Finish reason
            if (choice.TryGetProperty("finish_reason", out var finishReason))
            {
                response.FinishReason = finishReason.GetString();
            }

            return response;
        }
    }
}
