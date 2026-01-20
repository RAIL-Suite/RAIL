using System.Text.Json;
using System.Text.RegularExpressions;

namespace WpfRagApp.Services.ReAct;

/// <summary>
/// Parses LLM responses in ReAct format.
/// </summary>
public class ReActParser
{
    // Regex patterns for parsing ReAct format
    private static readonly Regex ThoughtPattern = new(
        @"Thought:\s*(.+?)(?=Action:|$)", 
        RegexOptions.Singleline | RegexOptions.IgnoreCase);
    
    private static readonly Regex ActionPattern = new(
        @"Action:\s*(.+?)(?=Thought:|Observation:|Answer:|$)", 
        RegexOptions.Singleline | RegexOptions.IgnoreCase);
    
    private static readonly Regex AnswerPattern = new(
        @"Answer:\s*(.+)$", 
        RegexOptions.Singleline | RegexOptions.IgnoreCase);
    
    private static readonly Regex FunctionCallPattern = new(
        @"^([\w\.]+)\s*\((.*)?\)$", 
        RegexOptions.Singleline);
    
    private static readonly Regex ParameterPattern = new(
        @"(\w+)\s*=\s*(?:""([^""]*)""|'([^']*)'|(\d+(?:\.\d+)?)|(\w+))",
        RegexOptions.Singleline);

    /// <summary>
    /// Parse a complete LLM response into a ReActStep.
    /// </summary>
    public ReActStep Parse(string llmResponse)
    {
        var step = new ReActStep();

        // Extract Thought
        var thoughtMatch = ThoughtPattern.Match(llmResponse);
        if (thoughtMatch.Success)
        {
            step.Thought = thoughtMatch.Groups[1].Value.Trim();
        }
        else
        {
            step.Thought = "(No explicit reasoning provided)";
        }

        // Extract Action
        var actionMatch = ActionPattern.Match(llmResponse);
        if (actionMatch.Success)
        {
            var actionText = actionMatch.Groups[1].Value.Trim();
            step.Action = ParseAction(actionText);
        }
        else
        {
            step.Action = new ReActAction { Type = ReActActionType.Invalid };
        }

        // Extract Answer (if FINISH)
        if (step.Action.Type == ReActActionType.Finish)
        {
            var answerMatch = AnswerPattern.Match(llmResponse);
            if (answerMatch.Success)
            {
                step.Action.Answer = answerMatch.Groups[1].Value.Trim();
            }
        }

        return step;
    }

    /// <summary>
    /// Parse an action string into a ReActAction.
    /// </summary>
    public ReActAction ParseAction(string actionText)
    {
        var action = new ReActAction { RawText = actionText };

        // Check for FINISH
        if (actionText.Trim().Equals("FINISH", StringComparison.OrdinalIgnoreCase))
        {
            action.Type = ReActActionType.Finish;
            return action;
        }

        // Try to parse as function call
        var funcMatch = FunctionCallPattern.Match(actionText.Trim());
        if (funcMatch.Success)
        {
            action.Type = ReActActionType.FunctionCall;
            action.FunctionName = funcMatch.Groups[1].Value;
            
            var paramsText = funcMatch.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(paramsText))
            {
                action.Parameters = ParseParameters(paramsText);
            }
            
            return action;
        }

        // Could not parse - invalid format
        action.Type = ReActActionType.Invalid;
        return action;
    }

    /// <summary>
    /// Parse function parameters from a parameter string.
    /// </summary>
    public Dictionary<string, object> ParseParameters(string paramText)
    {
        var parameters = new Dictionary<string, object>();

        foreach (Match match in ParameterPattern.Matches(paramText))
        {
            var paramName = match.Groups[1].Value;
            
            // Try to find the value (could be in different groups)
            string? stringValue = null;
            if (match.Groups[2].Success) stringValue = match.Groups[2].Value; // double quoted
            else if (match.Groups[3].Success) stringValue = match.Groups[3].Value; // single quoted
            else if (match.Groups[4].Success) // number
            {
                var numStr = match.Groups[4].Value;
                if (numStr.Contains('.'))
                    parameters[paramName] = double.Parse(numStr);
                else
                    parameters[paramName] = int.Parse(numStr);
                continue;
            }
            else if (match.Groups[5].Success) stringValue = match.Groups[5].Value; // unquoted word

            if (stringValue != null)
            {
                // Try to parse as bool
                if (bool.TryParse(stringValue, out var boolVal))
                    parameters[paramName] = boolVal;
                // Try to parse as int
                else if (int.TryParse(stringValue, out var intVal))
                    parameters[paramName] = intVal;
                else
                    parameters[paramName] = stringValue;
            }
        }

        return parameters;
    }

    /// <summary>
    /// Convert parameters dictionary to JSON string for function execution.
    /// </summary>
    public string ParametersToJson(Dictionary<string, object> parameters)
    {
        return JsonSerializer.Serialize(parameters);
    }

    /// <summary>
    /// Check if response appears to be in ReAct format.
    /// </summary>
    public bool IsReActFormat(string response)
    {
        return response.Contains("Thought:", StringComparison.OrdinalIgnoreCase) &&
               response.Contains("Action:", StringComparison.OrdinalIgnoreCase);
    }
}





