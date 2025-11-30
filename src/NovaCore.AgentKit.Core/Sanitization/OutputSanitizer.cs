using System.Text.RegularExpressions;

namespace NovaCore.AgentKit.Core.Sanitization;

/// <summary>
/// Sanitizes LLM output by removing thinking tags, unwrapping JSON, etc.
/// </summary>
public partial class OutputSanitizer : IOutputSanitizer
{
    // Thinking tag patterns (multiple formats)
    [GeneratedRegex(@"<thinking>.*?</thinking>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkingTagPattern();
    
    [GeneratedRegex(@"<internal_thoughts>.*?</internal_thoughts>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex InternalThoughtsPattern();
    
    [GeneratedRegex(@"<reasoning>.*?</reasoning>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ReasoningPattern();
    
    // Markdown JSON code block pattern
    [GeneratedRegex(@"```json\s*\n(.*?)\n```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownJsonPattern();
    
    // Generic markdown code block (fallback)
    [GeneratedRegex(@"```\s*\n(.*?)\n```", RegexOptions.Singleline)]
    private static partial Regex MarkdownCodePattern();
    
    public string Sanitize(string output, SanitizationOptions options)
    {
        if (string.IsNullOrEmpty(output))
            return output;
        
        var result = output;
        
        // Remove thinking tags
        if (options.RemoveThinkingTags)
        {
            result = ThinkingTagPattern().Replace(result, string.Empty);
            result = InternalThoughtsPattern().Replace(result, string.Empty);
            result = ReasoningPattern().Replace(result, string.Empty);
        }
        
        // Unwrap JSON from markdown
        if (options.UnwrapJsonFromMarkdown)
        {
            result = UnwrapJsonFromMarkdown(result);
        }
        
        // Remove null characters
        if (options.RemoveNullCharacters)
        {
            result = result.Replace("\0", string.Empty);
        }
        
        // Trim whitespace
        if (options.TrimWhitespace)
        {
            result = result.Trim();
        }
        
        return result;
    }
    
    private string UnwrapJsonFromMarkdown(string text)
    {
        // Try JSON-specific code block first
        var match = MarkdownJsonPattern().Match(text);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        
        // Try generic code block (but only if looks like JSON)
        match = MarkdownCodePattern().Match(text);
        if (match.Success)
        {
            var content = match.Groups[1].Value.Trim();
            if (content.StartsWith("{") || content.StartsWith("["))
            {
                return content;
            }
        }
        
        return text;
    }
}

