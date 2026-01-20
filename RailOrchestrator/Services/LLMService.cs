using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using RailFactory.Core;
using WpfRagApp.Services.ReAct;
using WpfRagApp.Models;
using WpfRagApp.Services.Abstractions;
using WpfRagApp.Services.Providers;

namespace WpfRagApp.Services
{
    /// <summary>
    /// Enterprise-grade LLM service with multi-provider support.
    /// Refactored to use ILLMProvider pattern.
    /// </summary>
    public class LLMService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;
        private readonly WpfRagApp.Services.Host.HostService? _hostService;
        
        // Providers
        private GeminiService? _geminiService; // Low-level service
        private GeminiProvider? _geminiProvider; // Adapter
        private OpenAIProvider? _openAiProvider; // Adapter
        private AnthropicProvider? _anthropicProvider; // Adapter
        
        // State
        private RailEngine? _engine;
        private string _toolsJson = "[]";
        private string _llmToolsJson = "[]";  // Cleaned version (kept for compatibility, though providers now adapt)
        private bool _isDisposed;
        private string? _currentApiProviderId;

        public bool IsCompositeManifest => _engine?.IsComposite ?? false;
        public string? CurrentAssetPath => _engine?.ArtifactPath;
        public int ModuleCount => _engine?.Registry?.ModuleCount ?? 0;
        public RailEngine? GetEngine() => _engine;
        public string? CurrentApiProviderId => _currentApiProviderId;
        public bool IsApiAsset => _currentApiProviderId != null;

        public LLMService(SettingsService settingsService, WpfRagApp.Services.Host.HostService? hostService = null)
        {
            _settingsService = settingsService;
            _hostService = hostService;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Factory method to get the correct provider based on model ID.
        /// </summary>
        private ILLMProvider GetProvider(string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) throw new ArgumentException("Model ID cannot be empty");

            if (modelId.StartsWith("gemini"))
            {
                var apiKey = _settingsService.GeminiApiKey;
                if (string.IsNullOrEmpty(apiKey)) throw new Exception("Gemini API Key is missing.");

                if (_geminiService == null) _geminiService = new GeminiService(apiKey);
                if (_geminiProvider == null) _geminiProvider = new GeminiProvider(_geminiService);
                
                return _geminiProvider;
            }
            else if (modelId.StartsWith("gpt") || modelId.StartsWith("o1"))
            {
                var apiKey = _settingsService.OpenAIApiKey;
                if (string.IsNullOrEmpty(apiKey)) throw new Exception("OpenAI API Key is missing.");

                if (_openAiProvider == null) _openAiProvider = new OpenAIProvider(apiKey, _httpClient);
                
                return _openAiProvider;
            }
            else if (modelId.StartsWith("claude"))
            {
                var apiKey = _settingsService.AnthropicApiKey;
                if (string.IsNullOrEmpty(apiKey)) throw new Exception("Anthropic API Key is missing.");

                if (_anthropicProvider == null) _anthropicProvider = new AnthropicProvider(apiKey, _httpClient);
                
                return _anthropicProvider;
            }

            throw new NotSupportedException($"Model '{modelId}' is not supported by any configured provider.");
        }

        public void SetAsset(string path)
        {
            DisposeEngine();
            _currentApiProviderId = null;

            if (string.IsNullOrEmpty(path))
            {
                _engine = null;
                _toolsJson = "[]";
                _llmToolsJson = "[]";
                return;
            }

            try
            {
                var apiManifestPath = System.IO.Path.Combine(path, "api.manifest.json");
                if (System.IO.File.Exists(apiManifestPath))
                {
                    LoadApiAsset(path);
                }
                else
                {
                    LoadExeAsset(path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LLMService] Error loading asset: {ex.Message}");
                _engine = null;
            }
        }
        
        private void LoadApiAsset(string path)
        {
            var providerId = System.IO.Path.GetFileName(path);
            _currentApiProviderId = providerId;
            _engine = null;
            
            var vectorService = WpfRagApp.Services.ApiOrchestration.ApiOrchestrationFactory.GetVectorService();
            vectorService.InitializeAsync().Wait();
            
            var skills = vectorService.GetProviderSkillsAsync(providerId).Result;
            
            if (skills.Count == 0)
            {
                _toolsJson = "[]";
                _llmToolsJson = "[]";
                return;
            }
            
            // Standardize tools for internal storage. Providers will adapt this later.
            var tools = skills.Select(skill => 
            {
                var props = new Dictionary<string, object>();
                var reqList = new List<string>();
                
                if (skill.Parameters != null)
                {
                    foreach (var p in skill.Parameters)
                    {
                        string type = p.Type?.ToLower() switch
                        {
                             "integer" or "int" or "int32" or "int64" => "integer",
                             "number" or "float" or "double" => "number",
                             "boolean" or "bool" => "boolean",
                             "array" => "array",
                             "object" => "object",
                             _ => "string"
                        };
                        
                        props[p.Name] = new { type = type, description = p.Description ?? "" };
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
        }
        
        private void LoadExeAsset(string path)
        {
            _engine = new RailEngine(path);
            _toolsJson = _engine.Load();
            _llmToolsJson = CleanToolsForLLM(_toolsJson); // Kept for backward compat, though providers adapt
        }

        private string CleanToolsForLLM(string toolsJson)
        {
             try 
             {
                 var node = JsonNode.Parse(toolsJson);
                 JsonArray? toolsArray = null;

                 if (node is JsonArray arr)
                 {
                     toolsArray = arr;
                 }
                 else if (node is JsonObject obj && obj.TryGetPropertyValue("tools", out var toolsNode) && toolsNode is JsonArray innerArr)
                 {
                     toolsArray = innerArr;
                 }

                 if (toolsArray != null)
                 {
                     foreach (var tool in toolsArray)
                     {
                         if (tool is JsonObject toolObj)
                         {
                             toolObj.Remove("class");
                         }
                     }
                     return node?.ToJsonString() ?? toolsJson;
                 }

                 return toolsJson;
             }
             catch
             {
                 return toolsJson;
             }
        }

        public async Task<string> ChatAsync(string message, string modelId, double temperature = 1.0)
        {
            ThrowIfDisposed();

            try
            {
                var provider = GetProvider(modelId);

                // Use ReAct Orchestrator if enabled (RECOMMENDED for all providers now)
                if (_settingsService.ReActEnabled)
                {
                    return await ChatWithReActAsync(message, modelId, temperature);
                }

                // Standard Chat (No Agent Loop)
                var history = new List<ProviderMessage>
                {
                    new ProviderMessage { Role = "user", Content = message }
                };

                var config = new ProviderConfig { ModelId = modelId, Temperature = temperature };
                var response = await provider.ChatAsync(history, _llmToolsJson, config);

                return response.TextContent ?? "No response";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public async Task<string> ChatWithAudioAsync(byte[] audioBytes, string modelId, double temperature = 1.0)
        {
            ThrowIfDisposed();
            
            try
            {
                var provider = GetProvider(modelId);
                
                // 1. Transcribe (if supported directly or we fallback)
                // If provider is multimodal (Gemini), we COULD send audio directly in history.
                // But current ReActOrchestrator assumes text query.
                // So we stick to "Transcribe -> Text Chat" workflow for all providers.
                
                var transcription = await provider.TranscribeAudioAsync(audioBytes);
                
                if (string.IsNullOrWhiteSpace(transcription) || 
                    transcription.Contains("no discernible speech", StringComparison.OrdinalIgnoreCase))
                {
                    return "🎤 Nessun audio rilevato. Riprova parlando più forte.";
                }

                // 2. Chat with transcription
                return await ChatAsync($"🎤 {transcription}", modelId, temperature);
            }
            catch (NotImplementedException)
            {
                return "Audio input not supported by this model provider.";
            }
            catch (Exception ex)
            {
                return $"Audio processing error: {ex.Message}";
            }
        }

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

            var apiToolHandler = IsApiAsset 
                ? WpfRagApp.Services.ApiOrchestration.ApiOrchestrationFactory.GetToolHandler() 
                : null;
            
            var provider = GetProvider(modelId);
            
            // Inject Provider instead of GeminiService
            var orchestrator = new ReActOrchestrator(provider, _engine, _hostService, config, apiToolHandler);
            
            try
            {
                var session = await orchestrator.ExecuteAsync(message, _llmToolsJson, modelId);
                
                // Format output
                var sb = new StringBuilder();
                foreach (var step in session.Steps)
                {
                     sb.AppendLine($"💭 **Thought:** {step.Thought}");
                     if (step.Action.Type == ReActActionType.FunctionCall)
                     {
                         sb.AppendLine($"⚙️ **Action:** {step.Action.FunctionName}");
                         if (!string.IsNullOrEmpty(step.Observation))
                         {
                             var obs = step.Observation.Length > 200 ? step.Observation.Substring(0, 200) + "..." : step.Observation;
                             sb.AppendLine($"👁 **Observation:** {obs}");
                         }
                     }
                     sb.AppendLine();
                }

                if (!string.IsNullOrEmpty(session.FinalAnswer))
                {
                    sb.AppendLine("---");
                    sb.AppendLine($"✅ **Answer:** {session.FinalAnswer}");
                }
                else if (session.Status == ReActSessionStatus.MaxStepsReached)
                {
                    sb.AppendLine("---");
                    sb.AppendLine("⚠️ Maximum reasoning steps reached.");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"ReAct Error: {ex.Message}";
            }
        }

        public async Task<string> GetBulkExecutionPlanAsync(string userPrompt, string fileContent, string modelId, double temperature = 0.3)
        {
            // Bulk execution generally requires strong reasoning (Gemini Pro / GPT-4)
            // Ideally we'd move this logic to a generic "Planner" service.
            // For now, we reuse the prompt via the provider.
            
            try 
            {
                var provider = GetProvider(modelId);
                
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

RESPOND WITH VALID JSON ONLY. NO MARKDOWN, NO EXPLANATION.";

                var history = new List<ProviderMessage>
                {
                    new ProviderMessage { Role = "user", Content = prompt }
                };
                
                var response = await provider.ChatAsync(history, null, new ProviderConfig { ModelId = modelId, Temperature = temperature });
                return response.TextContent ?? "";
            }
            catch (Exception ex)
            {
                return $"Error generating bulk plan: {ex.Message}";
            }
        }

        private void DisposeEngine()
        {
            if (_engine != null)
            {
                try { _engine.Dispose(); } catch {} finally { _engine = null; }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(LLMService));
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            DisposeEngine();
            _httpClient?.Dispose();
            _isDisposed = true;
        }
    }
}





