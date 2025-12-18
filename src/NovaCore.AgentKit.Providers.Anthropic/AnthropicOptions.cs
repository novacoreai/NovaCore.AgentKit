namespace NovaCore.AgentKit.Providers.Anthropic;

/// <summary>
/// Configuration options for Anthropic Claude provider
/// </summary>
public class AnthropicOptions
{
    /// <summary>Anthropic API key (required)</summary>
    public required string ApiKey { get; set; }
    
    /// <summary>Model name (default: claude-sonnet-4-5-20250929)</summary>
    public string Model { get; set; } = AnthropicModels.ClaudeSonnet45;
    
    /// <summary>Maximum tokens to generate</summary>
    public int MaxTokens { get; set; } = 4096;
    
    /// <summary>Temperature (0.0 - 1.0)</summary>
    public double Temperature { get; set; } = 1.0;
    
    /// <summary>Top P sampling</summary>
    public double? TopP { get; set; }
    
    /// <summary>Top K sampling</summary>
    public int? TopK { get; set; }
    
    // Anthropic-specific features
    
    /// <summary>Enable extended thinking mode (for Claude models that support it)</summary>
    public bool UseExtendedThinking { get; set; } = false;
    
    /// <summary>Thinking budget in tokens (for extended thinking mode)</summary>
    public int? ThinkingBudgetTokens { get; set; }
    
    /// <summary>Enable prompt caching to reduce costs for repetitive contexts</summary>
    public bool EnablePromptCaching { get; set; } = false;
    
    /// <summary>API base URL (optional, for custom endpoints)</summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>HTTP timeout</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
}

