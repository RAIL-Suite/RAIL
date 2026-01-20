namespace WpfRagApp.Services.ReAct;

/// <summary>
/// Configuration for ReAct behavior.
/// </summary>
public class ReActConfig
{
    /// <summary>
    /// Maximum number of reasoning steps before forcing termination.
    /// </summary>
    public int MaxSteps { get; set; } = 10;

    /// <summary>
    /// Temperature for LLM calls (lower = more deterministic).
    /// </summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>
    /// Default model to use.
    /// </summary>
    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// Enable automatic self-correction on function errors.
    /// </summary>
    public bool EnableSelfCorrection { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts for failed function calls.
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Timeout for each step.
    /// </summary>
    public TimeSpan StepTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to log detailed reasoning steps.
    /// </summary>
    public bool VerboseLogging { get; set; } = true;

    /// <summary>
    /// The ReAct system prompt template.
    /// </summary>
    public string SystemPromptTemplate { get; set; } = DEFAULT_SYSTEM_PROMPT;

    public const string DEFAULT_SYSTEM_PROMPT = @"You are an AI assistant using the ReAct (Reasoning + Acting) framework.

AVAILABLE TOOLS:
{tools}

FORMAT - You MUST follow this structure EXACTLY:

Thought: [Your reasoning about what to do next. Analyze the situation, what you know, what you need.]
Action: FunctionName(param1=""value1"", param2=""value2"")

After receiving an Observation, continue with:

Thought: [Analyze the observation. What did you learn? What's the next step?]
Action: NextFunction(...) OR FINISH

When you have ALL information needed to fully answer:

Thought: [Summarize what you learned and your conclusion]
Action: FINISH
Answer: [Your complete, helpful response to the user in English]

CRITICAL RULES:
1. ALWAYS write Thought before EVERY Action
2. Only ONE Action per response
3. Use EXACT function names from available tools
4. Parameter values must match expected types (STRING, INTEGER, etc.)
5. If an Action fails, analyze the error carefully in your next Thought and correct it
6. Use FINISH only when you have gathered ALL information needed
7. Answer in English always
8. Be thorough - for complex queries, call multiple functions to get complete information";
}// the same language as the user's query





