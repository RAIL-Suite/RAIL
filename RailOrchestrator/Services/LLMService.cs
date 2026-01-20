using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RailFactory.Core;
using WpfRagApp.Services.ReAct;
using WpfRagApp.Models;

namespace WpfRagApp.Services
{
    /// <summary>
    /// Enterprise-grade LLM service with multi-provider support.
    /// 
    /// ARCHITECTURE:
    /// - Polyglot: Supports Gemini, OpenAI, Anthropic
    /// - Agentic: Multi-step function calling with RailEngine
    /// - Resource-safe: Proper IDisposable for RailEngine lifecycle
    /// 
    /// COMPOSITE MANIFEST SUPPORT:
    /// When loading a composite manifest, RailEngine automatically:
    /// 1. Returns flat tool list with module prefixes to LLM
    /// 2. Routes function calls to correct module via Module.Function addressing
    /// </summary>
    public class LLMService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;
        private readonly WpfRagApp.Services.Host.HostService? _hostService;
        private GeminiService? _geminiService;
        private RailEngine? _engine;
        private string _toolsJson = "[]";
        private string _llmToolsJson = "[]";  // Cleaned version for LLM API (no 'class' field)
        private bool _isDisposed;

        /// <summary>
        /// Indicates if a composite manifest is currently loaded.
        /// </summary>
        public bool IsCompositeManifest => _engine?.IsComposite ?? false;
        
        /// <summary>
        /// Current artifact path, if any.
        /// </summary>
        public string? CurrentAssetPath => _engine?.ArtifactPath;
        
        /// <summary>
        /// Number of modules in composite manifest (0 for single).
        /// </summary>
        public int ModuleCount => _engine?.Registry?.ModuleCount ?? 0;
        
        /// <summary>
        /// Gets the current RailEngine instance for bulk execution.
        /// </summary>
        public RailEngine? GetEngine() => _engine;

        public LLMService(SettingsService settingsService, WpfRagApp.Services.Host.HostService? hostService = null)
        {
            _settingsService = settingsService;
            _hostService = hostService;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Sets the current asset (EXE artifact directory or API provider).
        /// For EXE: Loads RailEngine with tools
        /// For API: Loads skills from VectorService
        /// </summary>
        public void SetAsset(string path)
        {
            // Dispose previous engine to prevent resource leak
            DisposeEngine();
            _currentApiProviderId = null;

            if (string.IsNullOrEmpty(path))
            {
                // Chat Only mode - no tools
                _engine = null;
                _toolsJson = "[]";
                _llmToolsJson = "[]";
                System.Diagnostics.Debug.WriteLine("[LLMService] Chat Only mode - no tools loaded");
                return;
            }

            try
            {
                // Check if this is an API asset (has api.manifest.json)
                var apiManifestPath = System.IO.Path.Combine(path, "api.manifest.json");
                if (System.IO.File.Exists(apiManifestPath))
                {
                    // API Asset - load skills from VectorService
                    LoadApiAsset(path);
                }
                else
                {
                    // EXE Asset - use RailEngine
                    LoadExeAsset(path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LLMService] Error loading asset: {ex.Message}");
                _engine = null;
                _toolsJson = "[]";
                _llmToolsJson = "[]";
            }
        }
        
        private string? _currentApiProviderId;
        
        /// <summary>
        /// Current API provider ID if an API asset is selected.
        /// </summary>
        public string? CurrentApiProviderId => _currentApiProviderId;
        
        /// <summary>
        /// True if current asset is an API provider.
        /// </summary>
        public bool IsApiAsset => _currentApiProviderId != null;
        
        private void LoadApiAsset(string path)
        {
            var providerId = System.IO.Path.GetFileName(path);
            _currentApiProviderId = providerId;
            _engine = null; // No RailEngine for API assets
            
            // Load skills from VectorService synchronously
            var vectorService = WpfRagApp.Services.ApiOrchestration.ApiOrchestrationFactory.GetVectorService();
            vectorService.InitializeAsync().Wait();
            
            var skills = vectorService.GetProviderSkillsAsync(providerId).Result;
            
            if (skills.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[LLMService] API Asset '{providerId}' has no skills");
                _toolsJson = "[]";
                _llmToolsJson = "[]";
                return;
            }
            
            // Convert skills to tool format for LLM
            var tools = skills.Select(skill => 
            {
                var props = new Dictionary<string, object>();
                var reqList = new List<string>();
                
                if (skill.Parameters != null)
                {
                    foreach (var p in skill.Parameters)
                    {
                        props[p.Name] = new { type = MapParameterType(p.Type), description = p.Description ?? "" };
                        if (p.Required) reqList.Add(p.Name);
                    }
                }
                
                return new
                {
                    name = skill.SkillId.Replace(".", "_").Replace("-", "_"),
                    description = skill.Metadata?.SummaryForLLM ?? skill.DisplayName ?? skill.SkillId,
                    parameters = new
                    {
                        type = "object",
                        properties = props,
                        required = reqList
                    }
                };
            }).ToList();
            
            _llmToolsJson = System.Text.Json.JsonSerializer.Serialize(tools);
            _toolsJson = _llmToolsJson;
            
            System.Diagnostics.Debug.WriteLine(
                $"[LLMService] Loaded API asset '{providerId}' with {skills.Count} skills");
        }
        
        private void LoadExeAsset(string path)
        {
            _engine = new RailEngine(path);
            _toolsJson = _engine.Load();
            _llmToolsJson = CleanToolsForLLM(_toolsJson);
            
            var manifestType = _engine.IsComposite ? "COMPOSITE" : "SINGLE";
            var moduleCount = _engine.Registry?.ModuleCount ?? 0;
            
            System.Diagnostics.Debug.WriteLine(
                $"[LLMService] Loaded {manifestType} manifest from {path}" +
                (_engine.IsComposite ? $" ({moduleCount} modules)" : ""));
        }
        
        private static string MapParameterType(string? type)
        {
            return type?.ToLower() switch
            {
                "integer" or "int" or "int32" or "int64" => "integer",
                "number" or "float" or "double" => "number",
                "boolean" or "bool" => "boolean",
                "array" => "array",
                "object" => "object",
                _ => "string"
            };
        }

        /// <summary>
        /// Removes the 'class' field from each tool in the JSON.
        /// Gemini API doesn't accept unknown fields in function_declarations.
        /// </summary>
        private string CleanToolsForLLM(string toolsJson)
        {
            if (string.IsNullOrWhiteSpace(toolsJson) || toolsJson == "[]")
                return toolsJson;

            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(toolsJson);
                if (node == null) return toolsJson;

                System.Text.Json.Nodes.JsonArray? toolsArray = null;

                // Case 1: Direct array of tools
                if (node is System.Text.Json.Nodes.JsonArray arr)
                {
                    toolsArray = arr;
                }
                // Case 2: Object with "tools" property
                else if (node is System.Text.Json.Nodes.JsonObject obj && 
                         obj.TryGetPropertyValue("tools", out var toolsProp) && 
                         toolsProp is System.Text.Json.Nodes.JsonArray innerArr)
                {
                    toolsArray = innerArr;
                }

                if (toolsArray == null) return toolsJson;

                // Remove 'class' from each tool
                foreach (var tool in toolsArray)
                {
                    if (tool is System.Text.Json.Nodes.JsonObject toolObj)
                    {
                        toolObj.Remove("class");
                        toolObj.Remove("Class");
                    }
                }

                // Return just the tools array for Gemini
                return toolsArray.ToJsonString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LLMService] Error cleaning tools for LLM: {ex.Message}");
                return toolsJson;
            }
        }

        /// <summary>
        /// Sends a chat message to the selected LLM provider.
        /// Automatically routes to appropriate provider based on model ID prefix.
        /// </summary>
        public async Task<string> ChatAsync(string message, string modelId, double temperature = 1.0)
        {
            ThrowIfDisposed();
            
            if (modelId.StartsWith("gemini"))
            {
                return await ChatWithGeminiAsync(message, modelId, temperature);
            }
            else if (modelId.StartsWith("gpt"))
            {
                return await ChatWithOpenAIAsync(message, modelId);
            }
            else if (modelId.StartsWith("claude"))
            {
                return await ChatWithAnthropicAsync(message, modelId);
            }

            return "Unknown model selected.";
        }
        
        /// <summary>
        /// Get execution plan for bulk file processing.
        /// LLM analyzes file and returns JSON plan without executing.
        /// </summary>
        public async Task<string> GetBulkExecutionPlanAsync(
            string userPrompt, 
            string fileContent, 
            string modelId, 
            double temperature = 0.3)
        {
            ThrowIfDisposed();
            
            var prompt = $@"{userPrompt}

{fileContent}

IMPORTANT: Do NOT execute any function. Instead, analyze the data and respond with a JSON execution plan.

Available functions:
{_llmToolsJson}

Respond with this exact JSON format:
{{
  ""operations"": [
    {{
      ""function"": ""FunctionName"",
      ""useBatch"": false,
      ""calls"": [
        {{ ""param1"": ""value1"", ""param2"": ""value2"" }},
        {{ ""param1"": ""value3"", ""param2"": ""value4"" }}
      ]
    }}
  ]
}}

If a batch function exists (like AddCustomers instead of AddCustomer), use:
{{
  ""operations"": [
    {{
      ""function"": ""AddCustomers"",
      ""useBatch"": true,
      ""args"": {{ ""customers"": [...] }}
    }}
  ]
}}

RESPOND WITH VALID JSON ONLY. NO MARKDOWN, NO EXPLANATION.";

            if (modelId.StartsWith("gemini"))
            {
                var apiKey = _settingsService.GeminiApiKey;
                if (string.IsNullOrEmpty(apiKey)) return "Gemini API Key is missing.";
                
                if (_geminiService == null)
                {
                    _geminiService = new GeminiService(apiKey);
                }
                
                return await _geminiService.GenerateContentAsync(prompt);
            }
            
            return "Bulk mode only supported with Gemini models.";
        }
        
        /// <summary>
        /// Chat with audio input (voice).
        /// Transcribes audio, then passes to normal chat with tools.
        /// </summary>
        public async Task<string> ChatWithAudioAsync(byte[] audioBytes, string modelId, double temperature = 1.0)
        {
            ThrowIfDisposed();
            
            if (!modelId.StartsWith("gemini"))
            {
                return "Audio input only supported with Gemini models.";
            }
            
            var apiKey = _settingsService.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return "Gemini API Key is missing.";
            
            if (_geminiService == null)
            {
                _geminiService = new GeminiService(apiKey);
            }
            
            try
            {
                // Step 1: Transcribe audio
                var audioBase64 = Convert.ToBase64String(audioBytes);
                var transcription = await _geminiService.TranscribeAudioAsync(audioBase64, modelId);
                
                if (string.IsNullOrWhiteSpace(transcription) || 
                    transcription.Contains("no discernible speech", StringComparison.OrdinalIgnoreCase))
                {
                    return "🎤 Nessun audio rilevato. Riprova parlando più forte.";
                }
                
                // Step 2: Pass transcription to normal chat (with ReAct/tools)
                var response = await ChatAsync($"🎤 {transcription}", modelId, temperature);
                return response;
            }
            catch (Exception ex)
            {
                return $"Audio processing error: {ex.Message}";
            }
        }

        /// <summary>
        /// Gemini chat with agentic function calling loop.
        /// Supports multi-step reasoning with tool execution.
        /// Now supports ReAct mode for explicit reasoning chains.
        /// </summary>
        private async Task<string> ChatWithGeminiAsync(string message, string modelId, double temperature = 1.0)
        {
            var apiKey = _settingsService.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return "Gemini API Key is missing.";

            if (_geminiService == null)
            {
                _geminiService = new GeminiService(apiKey);
            }

            // Check if ReAct mode is enabled
            if (_settingsService.ReActEnabled)
            {
                return await ChatWithReActAsync(message, modelId, temperature);
            }

            // Legacy function calling mode
            var history = new List<GeminiContent>
            {
                new GeminiContent
                {
                    Role = "user",
                    Parts = new List<GeminiPart> { new GeminiPart { Text = message } }
                }
            };

            StringBuilder logBuilder = new StringBuilder();
            int maxSteps = 10;
            int currentStep = 0;

            try
            {
                while (currentStep < maxSteps)
                {
                    currentStep++;
                    var responseContent = await _geminiService.ChatWithToolsAsync(history, _llmToolsJson, modelId, temperature);
                    history.Add(responseContent);

                    bool functionExecuted = false;

                    if (responseContent.Parts != null)
                    {
                        foreach (var part in responseContent.Parts)
                        {
                            if (part.FunctionCall != null)
                            {
                                string funcName = part.FunctionCall.Name;
                                string args = part.FunctionCall.Arguments?.ToString() ?? "{}";

                                logBuilder.AppendLine($"⚙️ Step {currentStep}: Executing '{funcName}'...");

                                try
                                {
                                    string result = ExecuteFunction(funcName, args);

                                    var functionResultMsg = new GeminiContent
                                    {
                                        Role = "function",
                                        Parts = new List<GeminiPart>
                                        {
                                            new GeminiPart
                                            {
                                                FunctionResponse = new GeminiFunctionResponse
                                                {
                                                    Name = funcName,
                                                    Response = new { result = result }
                                                }
                                            }
                                        }
                                    };
                                    history.Add(functionResultMsg);
                                    functionExecuted = true;
                                }
                                catch (Exception ex)
                                {
                                    logBuilder.AppendLine($"❌ Error in {funcName}: {ex.Message}");
                                    
                                    // Send error back to LLM for recovery
                                    var errorMsg = new GeminiContent
                                    {
                                        Role = "function",
                                        Parts = new List<GeminiPart>
                                        {
                                            new GeminiPart
                                            {
                                                FunctionResponse = new GeminiFunctionResponse
                                                {
                                                    Name = funcName,
                                                    Response = new { error = ex.Message }
                                                }
                                            }
                                        }
                                    };
                                    history.Add(errorMsg);
                                    functionExecuted = true;
                                }
                            }
                        }
                    }

                    if (functionExecuted) continue;

                    var textPart = responseContent.Parts?.Find(p => !string.IsNullOrEmpty(p.Text));
                    if (textPart != null)
                    {
                        logBuilder.AppendLine($"\n✅ AGENT: {textPart.Text}");
                        return logBuilder.ToString();
                    }
                    
                    break; 
                }
            }
            catch (Exception ex)
            {
                return $"Error calling Gemini: {ex.Message}";
            }

            return logBuilder.ToString();
        }

        /// <summary>
        /// Gemini chat using ReAct (Reasoning + Acting) methodology.
        /// Provides explicit reasoning chains for better determinism and transparency.
        /// </summary>
        private async Task<string> ChatWithReActAsync(string message, string modelId, double temperature = 1.0)
        {
            var config = new ReActConfig
            {
                MaxSteps = _settingsService.ReActMaxSteps,
                Temperature = temperature,
                Model = modelId,
                EnableSelfCorrection = true,
                VerboseLogging = true
            };

            // Get API tool handler for API asset execution
            var apiToolHandler = IsApiAsset 
                ? WpfRagApp.Services.ApiOrchestration.ApiOrchestrationFactory.GetToolHandler() 
                : null;
            
            var orchestrator = new ReActOrchestrator(_geminiService!, _engine, _hostService, config, apiToolHandler);
            
            // Build tools description for ReAct prompt
            var toolsDescription = _llmToolsJson;
            
            try
            {
                var session = await orchestrator.ExecuteAsync(message, toolsDescription, modelId);
                
                // Build response with reasoning chain
                var responseBuilder = new StringBuilder();
                
                // Show steps
                foreach (var step in session.Steps)
                {
                    responseBuilder.AppendLine($"💭 **Thought:** {step.Thought}");
                    
                    if (step.Action.Type == ReActActionType.FunctionCall)
                    {
                        responseBuilder.AppendLine($"⚙️ **Action:** {step.Action.FunctionName}");
                        if (!string.IsNullOrEmpty(step.Observation))
                        {
                            var obsPreview = step.Observation.Length > 200 
                                ? step.Observation.Substring(0, 200) + "..." 
                                : step.Observation;
                            responseBuilder.AppendLine($"👁 **Observation:** {obsPreview}");
                        }
                    }
                    responseBuilder.AppendLine();
                }
                
                // Add final answer
                if (!string.IsNullOrEmpty(session.FinalAnswer))
                {
                    responseBuilder.AppendLine("---");
                    responseBuilder.AppendLine($"✅ **Answer:** {session.FinalAnswer}");
                }
                else if (session.Status == ReActSessionStatus.MaxStepsReached)
                {
                    responseBuilder.AppendLine("---");
                    responseBuilder.AppendLine("⚠️ Maximum reasoning steps reached.");
                }
                
                return responseBuilder.ToString();
            }
            catch (Exception ex)
            {
                return $"ReAct Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Executes a function via RailEngine.
        /// Supports both single manifest (FunctionName) and composite manifest (Module__FunctionName encoded).
        /// 
        /// ENTERPRISE DESIGN:
        /// - Uses HostService for v2.0 connected clients
        /// - Falls back to legacy RailEngine path if no host clients
        /// - Decodes encoded function names from Gemini (__ → .)
        /// </summary>
        private string ExecuteFunction(string funcName, string argsJson)
        {
            // Decode function name if it was encoded for Gemini API
            var decodedFuncName = FunctionNameEncoder.Decode(funcName);
            
            // v2.0: Try executing via HostService first (for Bridge-connected apps)
            if (_hostService != null && _hostService.Clients.Count > 0)
            {
                try
                {
                    // Get first connected client
                    var clientId = _hostService.Clients.Keys.First();
                    
                    // Parse args
                    var argsDict = string.IsNullOrEmpty(argsJson) || argsJson == "{}" 
                        ? null 
                        : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson);
                    
                    // Execute async but wait synchronously (for compatibility)
                    var result = _hostService.ExecuteAsync(clientId, decodedFuncName, argsDict).GetAwaiter().GetResult();
                    return result;
                }
                catch (Exception ex)
                {
                    return $"Error: {ex.Message}";
                }
            }
            
            // Fallback: Legacy path via RailEngine
            if (_engine == null)
            {
                return "No connected application. Please ensure the target app is running with Ignite() called.";
            }

            var responseJson = _engine.Execute(decodedFuncName, argsJson);
            
            // Parse response to extract result
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("status", out var statusProp) && 
                    statusProp.GetString() == "error" &&
                    root.TryGetProperty("error", out var errorProp))
                {
                    return $"Error: {errorProp.GetString()}";
                }
                
                if (root.TryGetProperty("result", out var resultProp))
                {
                    return resultProp.ToString();
                }
                
                return responseJson;
            }
            catch
            {
                return responseJson;
            }
        }

        /// <summary>
        /// OpenAI chat (basic, no function calling yet).
        /// </summary>
        private async Task<string> ChatWithOpenAIAsync(string message, string modelId)
        {
            var apiKey = _settingsService.OpenAIApiKey;
            if (string.IsNullOrEmpty(apiKey)) return "OpenAI API Key is missing.";

            var requestBody = new
            {
                model = modelId,
                messages = new[]
                {
                    new { role = "user", content = message }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    return $"OpenAI Error: {response.StatusCode} - {json}";
                }

                using var doc = JsonDocument.Parse(json);
                var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                return content ?? "No content returned.";
            }
            catch (Exception ex)
            {
                return $"Error calling OpenAI: {ex.Message}";
            }
        }

        /// <summary>
        /// Anthropic chat (basic, no function calling yet).
        /// </summary>
        private async Task<string> ChatWithAnthropicAsync(string message, string modelId)
        {
            var apiKey = _settingsService.AnthropicApiKey;
            if (string.IsNullOrEmpty(apiKey)) return "Anthropic API Key is missing.";

            var requestBody = new
            {
                model = modelId,
                max_tokens = 1024,
                messages = new[]
                {
                    new { role = "user", content = message }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"Anthropic Error: {response.StatusCode} - {json}";
                }

                using var doc = JsonDocument.Parse(json);
                var content = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
                return content ?? "No content returned.";
            }
            catch (Exception ex)
            {
                return $"Error calling Anthropic: {ex.Message}";
            }
        }

        /// <summary>
        /// Disposes the current RailEngine instance.
        /// </summary>
        private void DisposeEngine()
        {
            if (_engine != null)
            {
                try
                {
                    _engine.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LLMService] Error disposing engine: {ex.Message}");
                }
                finally
                {
                    _engine = null;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(LLMService));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            DisposeEngine();
            _httpClient?.Dispose();
            _isDisposed = true;
        }
    }
}





