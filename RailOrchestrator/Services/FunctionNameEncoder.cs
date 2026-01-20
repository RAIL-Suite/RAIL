namespace WpfRagApp.Services;

/// <summary>
/// Enterprise-grade encoder for function names in composite manifests.
/// 
/// PURPOSE:
/// Gemini API requires function names to match pattern: ^[a-zA-Z_][a-zA-Z0-9_]*$
/// Composite manifests use "Module.Function" format which contains dots.
/// This encoder converts dots to double underscores for API compatibility.
/// 
/// NOTE: This is a duplicate of RailFactory.Core.FunctionNameEncoder.
/// Kept here for NuGet package compatibility until next SDK release.
/// 
/// ENCODING RULES:
/// - "__" in original → "____" (escape existing double underscores)
/// - "." → "__" (convert dots to double underscores)
/// 
/// DECODING RULES (reverse order):
/// - "____" → "__" (restore original double underscores)
/// - "__" → "." (restore dots)
/// </summary>
public static class FunctionNameEncoder
{
    private const string DotReplacement = "__";
    private const string EscapedDotReplacement = "____";
    
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
}





