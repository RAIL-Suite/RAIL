using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using WpfRagApp.Models;

namespace WpfRagApp.Services
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public GeminiService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        public async Task<GeminiContent> ChatWithToolsAsync(List<GeminiContent> history, string toolsJson, string modelId = "gemini-2.0-flash-exp", double temperature = 1.0)
        {
            var url = $"{BaseUrl}/models/{modelId}:generateContent?key={_apiKey}";

            // Parse toolsJson to see if we have valid tools
            JsonNode? toolsNode = null;
            bool hasTools = false;

            if (!string.IsNullOrWhiteSpace(toolsJson))
            {
                try
                {
                    var parsedNode = JsonNode.Parse(toolsJson);
                    
                    // Case 1: It's directly an array of tools
                    if (parsedNode is JsonArray arr)
                    {
                        toolsNode = arr;
                        hasTools = arr.Count > 0;
                    }
                    // Case 2: It's a manifest object containing a "tools" property
                    else if (parsedNode is JsonObject obj && obj.TryGetPropertyValue("tools", out var toolsProp) && toolsProp is JsonArray toolsArr)
                    {
                        toolsNode = toolsArr;
                        hasTools = toolsArr.Count > 0;
                    }
                }
                catch
                {
                    // Ignore parsing errors, treat as no tools
                }
            }

            object requestBody;

            if (hasTools)
            {
                // Gemini expects tools wrapped in "function_declarations"
                var toolsObj = new
                {
                    function_declarations = toolsNode
                };

                requestBody = new
                {
                    contents = history,
                    tools = new[] { toolsObj },
                    generationConfig = new
                    {
                        temperature = temperature
                    }
                };
            }
            else
            {
                // No tools, send simple request without "tools" property
                requestBody = new
                {
                    contents = history,
                    generationConfig = new
                    {
                        temperature = temperature
                    }
                };
            }

            // Serialize with ignore nulls
            var options = new JsonSerializerOptions 
            { 
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                // REMOVED CamelCase policy to preserve "function_declarations"
            };
            var jsonContent = JsonSerializer.Serialize(requestBody, options);

            // DEBUG LOGGING
            System.Diagnostics.Debug.WriteLine($"[Gemini Request]: {jsonContent}");

            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(url, content));

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[Gemini Error]: {response.StatusCode} - {error}");
                throw new Exception($"Gemini API Error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>();
            return result?.Candidates?[0]?.Content ?? new GeminiContent { Role = "model", Parts = new List<GeminiPart> { new GeminiPart { Text = "No response" } } };
        }

        public async Task<string> GenerateContentAsync(string prompt)
        {
            var history = new List<GeminiContent>
            {
                new GeminiContent { Role = "user", Parts = new List<GeminiPart> { new GeminiPart { Text = prompt } } }
            };
            var result = await ChatWithToolsAsync(history, null);
            return result?.Parts?[0]?.Text ?? "No response";
        }
        
        /// <summary>
        /// Generate content from audio input.
        /// Sends base64 audio to Gemini for transcription and response.
        /// </summary>
        public async Task<string> GenerateWithAudioAsync(string audioBase64, string modelId = "gemini-2.0-flash-exp", double temperature = 1.0)
        {
            var url = $"{BaseUrl}/models/{modelId}:generateContent?key={_apiKey}";
            
            // Build multimodal request with audio
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "audio/wav",
                                    data = audioBase64
                                }
                            },
                            new
                            {
                                text = "Please transcribe this audio and respond to what was said. If the user is asking a question, answer it. If giving a command, acknowledge it."
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = temperature
                }
            };
            
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var jsonContent = JsonSerializer.Serialize(requestBody, options);
            
            System.Diagnostics.Debug.WriteLine($"[Gemini Audio Request]: Sending {audioBase64.Length} chars of audio data");
            
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(url, content));
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[Gemini Audio Error]: {response.StatusCode} - {error}");
                return $"Audio API Error: {response.StatusCode}";
            }
            
            var result = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>();
            return result?.Candidates?[0]?.Content?.Parts?[0]?.Text ?? "No response from audio";
        }
        
        /// <summary>
        /// Transcribe audio only (no response generation).
        /// Returns just the text of what was said.
        /// </summary>
        public async Task<string> TranscribeAudioAsync(string audioBase64, string modelId = "gemini-2.0-flash-exp")
        {
            var url = $"{BaseUrl}/models/{modelId}:generateContent?key={_apiKey}";
            
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "audio/wav",
                                    data = audioBase64
                                }
                            },
                            new
                            {
                                text = "Transcribe this audio exactly. Return only the transcription, nothing else."
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1 // Low temperature for accurate transcription
                }
            };
            
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var jsonContent = JsonSerializer.Serialize(requestBody, options);
            
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsync(url, content));
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Transcription error: {response.StatusCode}";
            }
            
            var result = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>();
            return result?.Candidates?[0]?.Content?.Parts?[0]?.Text ?? "";
        }

        public async Task<List<float>> EmbedContentAsync(string text)
        {
            var url = $"{BaseUrl}/models/text-embedding-004:embedContent?key={_apiKey}";
            var requestBody = new
            {
                model = "models/text-embedding-004",
                content = new { parts = new[] { new { text = text } } }
            };

            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync(url, requestBody));
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>();
            return result?.Embedding?.Values ?? new List<float>();
        }

        public async Task<List<List<float>>> BatchEmbedContentAsync(List<string> texts)
        {
            var url = $"{BaseUrl}/models/text-embedding-004:batchEmbedContents?key={_apiKey}";
            var requests = new List<object>();
            foreach (var text in texts)
            {
                requests.Add(new
                {
                    model = "models/text-embedding-004",
                    content = new { parts = new[] { new { text = text } } }
                });
            }

            var requestBody = new { requests = requests };
            var response = await _retryPolicy.ExecuteAsync(() => _httpClient.PostAsJsonAsync(url, requestBody));
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GeminiBatchEmbedResponse>();
            var embeddings = new List<List<float>>();
            if (result?.Embeddings != null)
            {
                foreach (var emb in result.Embeddings)
                {
                    embeddings.Add(emb.Values ?? new List<float>());
                }
            }
            return embeddings;
        }
    }
}





