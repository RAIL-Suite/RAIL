using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WpfRagApp.Models;
using WpfRagApp.Services.Abstractions;

namespace WpfRagApp.Services.Providers
{
    public class GeminiProvider : ILLMProvider
    {
        private readonly GeminiService _geminiService;
        public string ProviderId => "google";
        public bool SupportsAudioInput => true;

        public GeminiProvider(GeminiService geminiService)
        {
            _geminiService = geminiService;
        }

        public async Task<ProviderResponse> ChatAsync(
            List<ProviderMessage> history, 
            string? toolsJson = null, 
            ProviderConfig? config = null, 
            CancellationToken ct = default)
        {
            // 1. Convert specific tools format if needed (Gemini expects "function_declarations" inside "tools")
            // The existing ReActOrchestrator passes raw tool list, GeminiService handles some wrapping but we should ensure it's correct here.
            // For now, we rely on GeminiService's existing logic which accepts the raw list.
            
            // 2. Map History to GeminiContent
            var geminiHistory = history.Select(m => new GeminiContent
            {
                Role = MapRole(m.Role),
                Parts = MapParts(m)
            }).ToList();

            // 3. Call Service
            var modelId = config?.ModelId ?? "gemini-2.0-flash-exp";
            var temperature = config?.Temperature ?? 1.0;
            
            // Note: GeminiService.ChatWithToolsAsync doesn't accept CancellationToken yet, 
            // but we can add it later. For now we just await.
            var response = await _geminiService.ChatWithToolsAsync(geminiHistory, toolsJson ?? "[]", modelId, temperature);

            // 4. Map Response back to ProviderResponse
            return MapResponse(response);
        }

        public async Task<string> TranscribeAudioAsync(byte[] audioData, CancellationToken ct = default)
        {
            var base64 = Convert.ToBase64String(audioData);
            return await _geminiService.TranscribeAudioAsync(base64);
        }

        // --- Mappers ---

        private string MapRole(string role)
        {
            return role.ToLower() switch
            {
                "assistant" => "model",
                "system" => "user", // Gemini doesn't strictly support system role in chat history the same way, usually merged or user.
                "fail" => "user",   // Fallback
                _ => role
            };
        }

        private List<GeminiPart> MapParts(ProviderMessage message)
        {
            var parts = new List<GeminiPart>();

            if (!string.IsNullOrEmpty(message.Content))
            {
                parts.Add(new GeminiPart { Text = message.Content });
            }

            // Handle multimodal (audio) if present
            // Note: Current generic model supports byte[] AudioData.
            // We need to implement this if the upstream provides it. 
            // For now, we assume text-based history from ReActOrchestrator.
            
            return parts;
        }

        private ProviderResponse MapResponse(GeminiContent content)
        {
            var response = new ProviderResponse();
            
            if (content?.Parts == null) return response;

            var textParts = new List<string>();
            
            foreach (var part in content.Parts)
            {
                // Text
                if (!string.IsNullOrEmpty(part.Text))
                {
                    textParts.Add(part.Text);
                }

                // Function Call
                if (part.FunctionCall != null)
                {
                    response.FunctionCalls.Add(new ProviderFunctionCall
                    {
                        Name = part.FunctionCall.Name,
                        Arguments = ConvertArguments(part.FunctionCall.Arguments)
                    });
                }
            }

            response.TextContent = string.Join("\n", textParts);
            return response;
        }

        private Dictionary<string, object?> ConvertArguments(JsonObject? args)
        {
            if (args == null) return new Dictionary<string, object?>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(args.ToJsonString()) 
                       ?? new Dictionary<string, object?>();
            }
            catch
            {
                return new Dictionary<string, object?>();
            }
        }
    }
}
