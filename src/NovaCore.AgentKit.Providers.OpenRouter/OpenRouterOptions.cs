namespace NovaCore.AgentKit.Providers.OpenRouter;

/// <summary>
/// Configuration options for OpenRouter provider
/// </summary>
public class OpenRouterOptions
{
    /// <summary>OpenRouter API key (required)</summary>
    public required string ApiKey { get; set; }
    
    /// <summary>Model name - can be any model supported by OpenRouter (default: anthropic/claude-3.5-sonnet)</summary>
    public string Model { get; set; } = OpenRouterModels.Claude35Sonnet;
    
    /// <summary>Maximum tokens to generate</summary>
    public int? MaxTokens { get; set; }
    
    /// <summary>Temperature (0.0 - 2.0)</summary>
    public double? Temperature { get; set; }
    
    /// <summary>Top P sampling</summary>
    public double? TopP { get; set; }
    
    /// <summary>Frequency penalty (-2.0 to 2.0)</summary>
    public double? FrequencyPenalty { get; set; }
    
    /// <summary>Presence penalty (-2.0 to 2.0)</summary>
    public double? PresencePenalty { get; set; }
    
    // OpenRouter-specific features
    
    /// <summary>Provider preferences (e.g., order: ["Anthropic", "OpenAI"])</summary>
    public List<string>? ProviderPreferences { get; set; }
    
    /// <summary>Enable fallback to other providers</summary>
    public bool AllowFallbacks { get; set; } = true;
    
    /// <summary>HTTP Referer header (required by OpenRouter for attribution)</summary>
    public string? HttpReferer { get; set; }
    
    /// <summary>App title for OpenRouter's rankings</summary>
    public string? AppTitle { get; set; }
    
    /// <summary>HTTP timeout</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
}

