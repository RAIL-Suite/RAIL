using System.Text;
using System.Text.Json;
using RailFactory.Core;
using WpfRagApp.Services.ApiOrchestration;
using WpfRagApp.Models;
using WpfRagApp.Services;
using WpfRagApp.Services.Host;

namespace WpfRagApp.Services.ReAct;

/// <summary>
/// Orchestrates the ReAct reasoning loop.
/// </summary>
public class ReActOrchestrator
{
    private readonly GeminiService _gemini;
    private readonly RailEngine? _engine;
    private readonly HostService? _hostService;
    private readonly ApiSkillToolHandler? _apiToolHandler;
    private readonly ReActParser _parser;
    private readonly ErrorAnalyzer _errorAnalyzer;
    private readonly ReActConfig _config;
    
    public event Action<ReActStep>? OnStepCompleted;
    public event Action<string>? OnLog;

    public ReActOrchestrator(
        GeminiService gemini, 
        RailEngine? engine,
        HostService? hostService = null,
        ReActConfig? config = null,
        ApiSkillToolHandler? apiToolHandler = null)
    {
        _gemini = gemini;
        _engine = engine;
        _hostService = hostService;
        _apiToolHandler = apiToolHandler;
        _parser = new ReActParser();
        _errorAnalyzer = new ErrorAnalyzer();
        _config = config ?? new ReActConfig();
    }

    /// <summary>
    /// Execute a ReAct session for the given user query.
    /// </summary>
    public async Task<ReActSession> ExecuteAsync(
        string userQuery,
        string toolsJson,
        string? modelId = null,
        CancellationToken ct = default)
    {
        var session = new ReActSession { UserQuery = userQuery };
        var history = BuildInitialHistory(userQuery, toolsJson);
        var model = modelId ?? _config.Model;
        
        Log($"[ReAct] Starting session for: {userQuery}");
        Log($"[ReAct] Max steps: {_config.MaxSteps}, Model: {model}");

        try
        {
            while (session.Steps.Count < _config.MaxSteps && !ct.IsCancellationRequested)
            {
                var stepStart = DateTime.Now;
                
                // Call LLM
                var response = await _gemini.ChatWithToolsAsync(
                    history, 
                    null, // We include tools in system prompt for ReAct
                    model, 
                    _config.Temperature);

                var responseText = response.Parts?.FirstOrDefault()?.Text ?? string.Empty;
                Log($"[ReAct] Step {session.Steps.Count + 1} response:\n{responseText}");

                ReActStep step;
                
                // Check if this is Generative PowerShell mode
                bool isGenerativePowerShell = toolsJson.Contains("\"runtime_type\": \"generative_powershell\"", StringComparison.OrdinalIgnoreCase);

                if (isGenerativePowerShell)
                {
                     // CUSTOM PARSER FOR DIRECT SCRIPTING
                     step = new ReActStep { Thought = responseText };
                     
                     // Regex to extract ```powershell ... ``` or just ``` ... ```
                     var match = System.Text.RegularExpressions.Regex.Match(responseText, @"```(?:powershell)?\s*(.*?)\s*```", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                     
                     if (match.Success)
                     {
                         var scriptContent = match.Groups[1].Value.Trim();
                         if (!string.IsNullOrWhiteSpace(scriptContent))
                         {
                             // Create synthetic FunctionCall action
                             step.Action = new ReActAction
                             {
                                 Type = ReActActionType.FunctionCall,
                                 FunctionName = "PowerShell.Execute",
                                 Parameters = new Dictionary<string, object> { { "script", scriptContent } }
                             };
                         }
                         else
                         {
                             // Empty code block?
                             step.Action = new ReActAction { Type = ReActActionType.Finish, Answer = responseText };
                         }
                     }
                     else if (responseText.Contains("DONE", StringComparison.OrdinalIgnoreCase) || 
                              (responseText.Length < 100 && (responseText.Contains("Fatto", StringComparison.OrdinalIgnoreCase) || responseText.Contains("Ecco", StringComparison.OrdinalIgnoreCase))))
                     {
                         // Treat as simple finish
                         step.Action = new ReActAction { Type = ReActActionType.Finish, Answer = responseText };
                     }
                     else
                     {
                         // No code block found.
                         // If the model is just talking, maybe it's asking for clarification or done.
                         // For now, treat as Finish.
                         step.Action = new ReActAction { Type = ReActActionType.Finish, Answer = responseText };
                     }
                }
                else
                {
                    // STANDARD REACT PARSING
                    step = _parser.Parse(responseText);
                }

                step.Duration = DateTime.Now - stepStart;
                
                // Handle based on action type
                switch (step.Action.Type)
                {
                    case ReActActionType.Finish:
                        session.AddStep(step);
                        session.FinalAnswer = step.Action.Answer ?? ExtractFinalAnswer(responseText);
                        session.Status = ReActSessionStatus.Completed;
                        session.EndTime = DateTime.Now;
                        OnStepCompleted?.Invoke(step);
                        Log($"[ReAct] Session completed with answer");
                        return session;

                    case ReActActionType.FunctionCall:
                        var observation = await ExecuteFunctionAsync(step.Action);
                        step.Observation = observation;
                        session.AddStep(step);
                        
                        // Add to history
                        history.Add(new GeminiContent
                        {
                            Role = "model",
                            Parts = new List<GeminiPart> { new GeminiPart { Text = responseText } }
                        });
                        history.Add(new GeminiContent
                        {
                            Role = "user",
                            Parts = new List<GeminiPart> { new GeminiPart { Text = $"Observation: {observation}" } }
                        });
                        
                        OnStepCompleted?.Invoke(step);
                        
                        // Check if error and add correction hint
                        if (_config.EnableSelfCorrection && _errorAnalyzer.ShouldRetry(observation))
                        {
                            var errorType = _errorAnalyzer.Classify(observation);
                            var hint = _errorAnalyzer.GenerateCorrectionHint(errorType, observation);
                            Log($"[ReAct] Error detected, adding correction hint");
                            
                            // Append hint to last message
                            var lastPart = history.Last().Parts?.First();
                            if (lastPart != null)
                            {
                                lastPart.Text += $"\n\n{hint}";
                            }
                        }
                        break;

                    case ReActActionType.Invalid:
                        Log($"[ReAct] Invalid action format, requesting correction");
                        session.AddStep(step);
                        
                        // Add correction request to history
                        history.Add(new GeminiContent
                        {
                            Role = "model",
                            Parts = new List<GeminiPart> { new GeminiPart { Text = responseText } }
                        });
                        history.Add(new GeminiContent
                        {
                            Role = "user",
                            Parts = new List<GeminiPart> { new GeminiPart { 
                                Text = "Your response format was incorrect. Please use the exact ReAct format:\n" +
                                       "Thought: [your reasoning]\n" +
                                       "Action: FunctionName(param=\"value\") OR Action: FINISH\n\n" +
                                       "Try again with the correct format."
                            }}
                        });
                        OnStepCompleted?.Invoke(step);
                        break;
                }
            }

            // Max steps reached
            session.Status = ReActSessionStatus.MaxStepsReached;
            session.EndTime = DateTime.Now;
            session.FinalAnswer = BuildMaxStepsAnswer(session);
            Log($"[ReAct] Max steps ({_config.MaxSteps}) reached");
        }
        catch (Exception ex)
        {
            Log($"[ReAct] Error: {ex.Message}");
            session.Status = ReActSessionStatus.Error;
            session.EndTime = DateTime.Now;
            session.FinalAnswer = $"An error occurred during reasoning: {ex.Message}";
        }

        return session;
    }

    /// <summary>
    /// Execute a function via RailEngine or ApiSkillToolHandler.
    /// </summary>
    private async Task<string> ExecuteFunctionAsync(ReActAction action)
    {
        var functionName = action.FunctionName?.ToLower() ?? "";
        
        // Route execute_api to ApiSkillToolHandler
        if (functionName == "execute_api" || functionName == "executeapi" || functionName == "api")
        {
            if (_apiToolHandler == null)
            {
                return "Error: API tool handler not available. Please configure API integrations.";
            }
            
            try
            {
                Log($"[ReAct] Executing API: {JsonSerializer.Serialize(action.Parameters)}");
                var argsJson = _parser.ParametersToJson(action.Parameters);
                return await _apiToolHandler.HandleAsync(argsJson);
            }
            catch (Exception ex)
            {
                Log($"[ReAct] API execution error: {ex.Message}");
                return $"Error executing API: {ex.Message}";
            }
        }
        
        // Route search_api to find matching skills
        if (functionName == "search_api" || functionName == "searchapi" || functionName == "find_api")
        {
            if (_apiToolHandler == null)
            {
                return "Error: API tool handler not available.";
            }
            
            try
            {
                var query = action.Parameters.GetValueOrDefault("query")?.ToString() ?? "";
                var providerId = action.Parameters.GetValueOrDefault("provider")?.ToString();
                return await _apiToolHandler.SearchSkillsAsync(query, providerId);
            }
            catch (Exception ex)
            {
                return $"Error searching APIs: {ex.Message}";
            }
        }
        
        // Check if this is an API skill call (has no RailEngine but has apiToolHandler)
        if (_engine == null && _apiToolHandler != null)
        {
            // Route to API executor for API skills
            try
            {
                // skillId is already in correct format (underscores) from LLM
                var skillId = action.FunctionName ?? "";
                Log($"[ReAct] Executing API skill: {skillId}");
                
                var args = JsonSerializer.Serialize(new 
                { 
                    skill_id = skillId, 
                    parameters = action.Parameters 
                });
                
                return await _apiToolHandler.HandleAsync(args);
            }
            catch (Exception ex)
            {
                return $"Error executing API skill: {ex.Message}";
            }
        }
        
        // Default: route to RailEngine for local exe functions
        if (_engine == null)
        {
            return "Error: No RailEngine available. Please select an asset.";
        }

        try
        {
            Log($"[ReAct] Executing: {action.FunctionName}({JsonSerializer.Serialize(action.Parameters)})");
            
            var argsJson = _parser.ParametersToJson(action.Parameters);
            
            // Decode function name if it was encoded for Gemini API
            // This reverses the encoding done in SerializeToolsForLLM:
            // "WorkflowDemo__GetProduct" → "WorkflowDemo.GetProduct"
            var decodedFunctionName = RailFactory.Core.FunctionNameEncoder.Decode(action.FunctionName!);
            
            string result;

            if (_hostService != null && _hostService.Clients.Count > 0)
            {
                // V2.0 Smart Routing: Use HostService to find the best client
                var session = _hostService.FindClientForMethod(decodedFunctionName);
                
                if (session != null)
                {
                    Log($"[ReAct] Routing execution to HostService (Client: {session.InstanceId})");
                    try
                    {
                       // Convert argsJson back to dictionary for HostService
                       var argsDict = string.IsNullOrEmpty(argsJson) || argsJson == "{}" 
                            ? null 
                            : JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson);
                            
                       result = await _hostService.ExecuteAsync(session.InstanceId, decodedFunctionName, argsDict);
                    }
                    catch (Exception hex)
                    {
                        Log($"[ReAct] HostService execution failed: {hex.Message}. Falling back to local engine.");
                        if (_engine != null)
                            result = _engine.Execute(decodedFunctionName, argsJson);
                        else
                            return $"Error: {hex.Message}";
                    }
                }
                else
                {
                    // No client found, try local engine
                    Log("[ReAct] No supporting client found on Host. Defaulting to local engine.");
                    result = _engine?.Execute(decodedFunctionName, argsJson) ?? "Error: No execution engine available.";
                }
            }
            else if (_engine != null)
            {
                // Local execution only
                result = _engine.Execute(decodedFunctionName, argsJson);
            }
            else
            {
                return "Error: No execution engine available.";
            }
            
            // Parse result to extract actual content
            try
            {
                using var doc = JsonDocument.Parse(result);
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
            }
            catch
            {
                // Not JSON, return as-is
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Log($"[ReAct] Function execution error: {ex.Message}");
            return $"Error executing {action.FunctionName}: {ex.Message}";
        }
    }

    /// <summary>
    /// Build initial conversation history with system prompt.
    /// </summary>
    private List<GeminiContent> BuildInitialHistory(string userQuery, string toolsJson)
    {
        // Build system prompt with tools
        bool isGenerativePowerShell = toolsJson.Contains("\"runtime_type\": \"generative_powershell\"", StringComparison.OrdinalIgnoreCase);

        // Build system prompt with tools
        // If Generative PowerShell, we do NOT inject tools. We inject instructions for Code Blocks.
        var systemPrompt = isGenerativePowerShell 
            ? _config.SystemPromptTemplate.Replace("{tools}", "NO TOOLS AVAILABLE. You are in Direct Scripting Mode.") 
            : _config.SystemPromptTemplate.Replace("{tools}", toolsJson);

        var initialMessages = new List<GeminiContent>();

        initialMessages.Add(new GeminiContent
        {
            Role = "user",
            Parts = new List<GeminiPart> 
            { 
                new GeminiPart { Text = $"{systemPrompt}\n\nUSER QUERY: {userQuery}" }
            }
        });

        if (isGenerativePowerShell)
        {
            // Extract appName if possible to give context (e.g. "Excel")
            string appName = "Application";
            string progId = "";
            
            if (toolsJson.Contains("\"appName\": \"Excel\"", StringComparison.OrdinalIgnoreCase)) 
            {
                appName = "Microsoft Excel";
                progId = "Excel.Application";
            }
            else if (toolsJson.Contains("\"appName\": \"Word\"", StringComparison.OrdinalIgnoreCase)) 
            {
                appName = "Microsoft Word";
                progId = "Word.Application";
            }
            else if (toolsJson.Contains("\"appName\": \"PowerPoint\"", StringComparison.OrdinalIgnoreCase)) 
            {
                appName = "Microsoft PowerPoint";
                progId = "PowerPoint.Application";
            }

            string injection = $@"
[SYSTEM INJECTION]
ACT AS: Expert Windows Automation Engineer specializing in COM/OLE Interop.
TARGET APPLICATION: {appName}

MANDATORY TECHNICAL CONSTRAINTS:
1. **DIRECT SCRIPTING MODE**: You are NOT using function calling. You MUST output a **SINGLE VALID POWERSHELL SCRIPT** inside a Markdown Code Block.
   Example:
   ```powershell
   $app = [Runtime.InteropServices.Marshal]::GetActiveObject('{progId}')
   $app.Visible = $true
   # ... rest of script ...
   ```
2. **NO FILE GENERATION**: Do NOT create new files on disk. Do NOT use libraries like OpenXML, EPPlus, or Import-Excel.
3. **LIVE ATTACHMENT**: You MUST attach to the EXISTING running process using `[Runtime.InteropServices.Marshal]::GetActiveObject('{progId}')`.
4. **INTERACTIVE SCOPE**: Operate EXCLUSIVELY on the user's current context:
   - Use `$app.Selection` to modify what the user has selected.
   - Use `$app.ActiveSheet` or `$app.ActiveDocument`.
5. **ERROR HANDLING**: If the application is not running, script MUST FAIL gracefully with ""Application not found"". Do NOT launch a hidden instance.
6. **UI CLEANED FOR USER***: When you create something pay always attention to create something usefull for the user, use always great and modern UI for the graphic result when the output si something that user can really see and use and understand.
7. **ENGLISH ALWAYS**: Always use English for the text block.
8. **CULTURAL ROBUSTNESS (CRITICAL FOR ITALIAN EXCEL)**:
   - **Dynamic Separator Detection**: NEVER hardcode dots (.) or commas (,) in format strings. ALWAYS detect the system separator dynamically.
     - WRONG: `$range.NumberFormat = '0.00%` (Fails in Italy)
     - RIGHT:
       ```powershell
       $s = [System.Globalization.CultureInfo]::CurrentCulture.NumberFormat.NumberDecimalSeparator
       $range.NumberFormat = '0' + $s + '00%'
       ```
   - **Safe Data Injection**: To avoid COM InvalidCastException, prefer passing Strings formatted with the local culture instead of raw Doubles.
     - WRONG: `$cell.Value2 = 0.12` (May crash)
     - RIGHT: `$cell.Value = (0.12).ToString([System.Globalization.CultureInfo]::CurrentCulture)`


GOAL: Output ONLY the code block.
";
            initialMessages[0].Parts[0].Text += "\n" + injection;
        }

        return initialMessages;
    }

    /// <summary>
    /// Extract final answer from response if not explicitly parsed.
    /// </summary>
    private string ExtractFinalAnswer(string responseText)
    {
        // Try to find Answer: section
        var answerIndex = responseText.IndexOf("Answer:", StringComparison.OrdinalIgnoreCase);
        if (answerIndex >= 0)
        {
            return responseText.Substring(answerIndex + 7).Trim();
        }
        
        // Fallback: return everything after FINISH
        var finishIndex = responseText.IndexOf("FINISH", StringComparison.OrdinalIgnoreCase);
        if (finishIndex >= 0)
        {
            return responseText.Substring(finishIndex + 6).Trim();
        }
        
        return responseText;
    }

    /// <summary>
    /// Build a summary answer when max steps is reached.
    /// </summary>
    private string BuildMaxStepsAnswer(ReActSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("⚠️ Maximum reasoning steps reached. Here's what I found:\n");
        
        foreach (var step in session.Steps.Where(s => !string.IsNullOrEmpty(s.Observation)))
        {
            sb.AppendLine($"• {step.Action.FunctionName}: {TruncateText(step.Observation!, 200)}");
        }
        
        return sb.ToString();
    }

    private string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength) + "...";
    }

    private void Log(string message)
    {
        if (_config.VerboseLogging)
        {
            System.Diagnostics.Debug.WriteLine(message);
            OnLog?.Invoke(message);
        }
    }
}





