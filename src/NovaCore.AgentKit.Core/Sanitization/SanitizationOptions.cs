namespace NovaCore.AgentKit.Core.Sanitization;

/// <summary>
/// Configuration options for output sanitization
/// </summary>
public class SanitizationOptions
{
    /// <summary>Remove thinking tags (Claude, GPT, Grok, etc.)</summary>
    public bool RemoveThinkingTags { get; set; } = true;
    
    /// <summary>Unwrap JSON from markdown code blocks</summary>
    public bool UnwrapJsonFromMarkdown { get; set; } = true;
    
    /// <summary>Trim whitespace</summary>
    public bool TrimWhitespace { get; set; } = true;
    
    /// <summary>Remove null characters</summary>
    public bool RemoveNullCharacters { get; set; } = true;
}

