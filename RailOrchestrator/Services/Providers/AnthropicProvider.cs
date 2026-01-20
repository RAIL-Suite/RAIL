using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfRagApp.Models;
using WpfRagApp.Services.Abstractions;

namespace WpfRagApp.Services.Providers
{
    public class AnthropicProvider : ILLMProvider
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        public string ProviderId => "anthropic";
        public bool SupportsAudioInput => false; 

        public AnthropicProvider(string apiKey, HttpClient? httpClient = null)
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
            var modelId = config?.ModelId ?? "claude-3-5-sonnet-20240620";
            var temperature = config?.Temperature ?? 0.7;

            // 1. Prepare Messages and System Prompt
            var messages = new List<object>();
            string systemPrompt = "";

            foreach (var msg in history)
            {
                if (msg.Role == "system")
                {
                    systemPrompt += msg.Content + "\n";
                }
                else
                {
                    messages.Add(MapMessage(msg));
                }
            }

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
                max_tokens = config?.MaxTokens ?? 4096,
                messages = messages,
                tools = tools,
                system = string.IsNullOrEmpty(systemPrompt) ? null : systemPrompt,
                temperature = temperature
            };

            // 4. Send Request
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            
            var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            var jsonContent = JsonSerializer.Serialize(requestBody, jsonOptions);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new Exception($"Anthropic API Error: {response.StatusCode} - {error}");
            }

            var resultJson = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(resultJson);
        }

        public Task<string> TranscribeAudioAsync(byte[] audioData, CancellationToken ct = default)
        {
            throw new NotImplementedException("Audio transcription not supported by Anthropic provider.");
        }

        // --- Adapters ---

        private object MapMessage(ProviderMessage msg)
        {
            if (msg.Role == "tool")
            {
                return new
                {
                    role = "user",
                    content = new object[] 
                    {
                        new 
                        {
                            type = "tool_result",
                            tool_use_id = msg.ToolCallId,
                            content = msg.Content
                        }
                    }
                };
            }
            
            if (msg.Role == "model") // Assistant
            {
                // If it's a simple text response
                if (msg.Content != null && msg.Content.TrimStart().StartsWith("{") == false) // Simple heuristic, or check structure
                {
                     return new { role = "assistant", content = msg.Content };
                }
                
                // Note: Reconstructing assistant tool_use blocks from flat history is tricky 
                // if we don't store the original structure.
                // For ReAct text-only history, standard content string is fine.
                return new { role = "assistant", content = msg.Content };
            }

            // User
            return new { role = "user", content = msg.Content };
        }

        /// <summary>
        /// Adapts RAIL tools (flat list) to Anthropic tools format.
        /// RAIL: [ { "name": "...", "parameters": ... } ]
        /// Anthropic: [ { "name": "...", "description": "...", "input_schema": ... } ]
        /// </summary>
        private List<object>? AdaptTools(string toolsJson)
        {
            try
            {
                var node = JsonNode.Parse(toolsJson);
                if (node is not JsonArray arr) return null;

                var anthropicTools = new List<object>();

                foreach (var tool in arr)
                {
                    if (tool is JsonObject toolObj)
                    {
                        // Extract fields
                        var name = toolObj["name"]?.ToString();
                        var description = toolObj["description"]?.ToString();
                        var parameters = toolObj["parameters"]; // This effectively becomes input_schema

                        if (name != null)
                        {
                            anthropicTools.Add(new
                            {
                                name = name,
                                description = description,
                                input_schema = parameters // RAIL parameters block is a valid JSON Schema object, which matches input_schema
                            });
                        }
                    }
                }

                return anthropicTools.Any() ? anthropicTools : null;
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
            
            // Finish reason
            if (root.TryGetProperty("stop_reason", out var stopReason))
            {
                response.FinishReason = stopReason.GetString();
            }

            // Usage
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("input_tokens", out var it)) response.InputTokens = it.GetInt32();
                if (usage.TryGetProperty("output_tokens", out var ot)) response.OutputTokens = ot.GetInt32();
            }

            // Content
            if (root.TryGetProperty("content", out var contentArray))
            {
                var textBuilder = new StringBuilder();
                
                foreach (var item in contentArray.EnumerateArray())
                {
                    var type = item.GetProperty("type").GetString();
                    
                    if (type == "text")
                    {
                        textBuilder.Append(item.GetProperty("text").GetString());
                    }
                    else if (type == "tool_use")
                    {
                        var id = item.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                        var name = item.GetProperty("name").GetString() ?? "";
                        var input = item.GetProperty("input"); // This is a JSON object already
                        
                        var argsDict = new Dictionary<string, object?>();
                        try 
                        { 
                            argsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(input.GetRawText()) 
                                       ?? new Dictionary<string, object?>();
                        } catch {}

                        response.FunctionCalls.Add(new ProviderFunctionCall
                        {
                            Id = id,
                            Name = name,
                            Arguments = argsDict
                        });
                    }
                }
                
                response.TextContent = textBuilder.ToString();
            }

            return response;
        }
    }
}
