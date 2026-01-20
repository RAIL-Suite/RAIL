namespace RailFactory.Core;

/// <summary>
/// Enterprise-grade encoder for function names in composite manifests.
/// 
/// PURPOSE:
/// Gemini API requires function names to match pattern: ^[a-zA-Z_][a-zA-Z0-9_]*$
/// Composite manifests use "Module.Function" format which contains dots.
/// This encoder converts dots to double underscores for API compatibility.
/// 
/// ENCODING RULES:
/// - "__" in original → "____" (escape existing double underscores)
/// - "." → "__" (convert dots to double underscores)
/// 
/// DECODING RULES (reverse order):
/// - "____" → "__" (restore original double underscores)
/// - "__" → "." (restore dots)
/// 
/// EXAMPLES:
/// - "WorkflowDemo.GetProduct" → "WorkflowDemo__GetProduct"
/// - "my__class.method" → "my____class__method"
/// - "a.b.c" → "a__b__c"
/// 
/// THREAD SAFETY: All methods are stateless and thread-safe.
/// PERFORMANCE: O(n) where n is string length.
/// </summary>
public static class FunctionNameEncoder
{
    private const string DotReplacement = "__";
    private const string EscapedDotReplacement = "____";
    
    /// <summary>
    /// Encodes a function name for Gemini API compatibility.
    /// Converts dots to double underscores while preserving original underscores.
    /// </summary>
    /// <param name="functionName">Original function name (may contain dots)</param>
    /// <returns>Encoded name safe for Gemini API</returns>
    public static string Encode(string functionName)
    {
        if (string.IsNullOrEmpty(functionName))
            return functionName;
        
        // Step 1: Escape existing "__" sequences
        // This prevents ambiguity during decoding
        var escaped = functionName.Replace(DotReplacement, EscapedDotReplacement);
        
        // Step 2: Convert dots to "__"
        var encoded = escaped.Replace(".", DotReplacement);
        
        return encoded;
    }
    
    /// <summary>
    /// Decodes a function name back to original format.
    /// Reverses the encoding process.
    /// </summary>
    /// <param name="encodedName">Encoded function name from Gemini</param>
    /// <returns>Original function name with dots restored</returns>
    public static string Decode(string encodedName)
    {
        if (string.IsNullOrEmpty(encodedName))
            return encodedName;
        
        // Must process in correct order to handle overlapping patterns
        // Use a character-by-character approach for correctness
        var result = new System.Text.StringBuilder();
        int i = 0;
        
        while (i < encodedName.Length)
        {
            // Check for "____" (escaped double underscore - was original "__")
            if (i + 4 <= encodedName.Length && 
                encodedName.Substring(i, 4) == EscapedDotReplacement)
            {
                result.Append(DotReplacement); // Restore "__"
                i += 4;
            }
            // Check for "__" (encoded dot)
            else if (i + 2 <= encodedName.Length && 
                     encodedName.Substring(i, 2) == DotReplacement)
            {
                result.Append('.'); // Restore "."
                i += 2;
            }
            else
            {
                result.Append(encodedName[i]);
                i++;
            }
        }
        
        return result.ToString();
    }
    
    /// <summary>
    /// Checks if a function name requires encoding (contains dots or double underscores).
    /// </summary>
    public static bool RequiresEncoding(string functionName)
    {
        if (string.IsNullOrEmpty(functionName))
            return false;
        
        return functionName.Contains('.') || functionName.Contains(DotReplacement);
    }
    
    /// <summary>
    /// Validates that an encoded name is safe for Gemini API.
    /// Pattern: ^[a-zA-Z_][a-zA-Z0-9_]*$
    /// </summary>
    public static bool IsValidGeminiName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        
        // First character must be letter or underscore
        char first = name[0];
        if (!char.IsLetter(first) && first != '_')
            return false;
        
        // Remaining characters must be alphanumeric or underscore
        for (int i = 1; i < name.Length; i++)
        {
            char c = name[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }
        
        return true;
    }
}



