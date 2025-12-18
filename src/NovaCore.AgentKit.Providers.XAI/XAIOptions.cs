namespace NovaCore.AgentKit.Providers.XAI;

/// <summary>
/// Configuration options for xAI Grok provider.
/// xAI API is OpenAI-compatible and uses base URL: https://api.x.ai/v1
/// For full API documentation, see: https://docs.x.ai/docs/api-reference
/// </summary>
public class XAIOptions
{
    /// <summary>xAI API key (required)</summary>
    public required string ApiKey { get; set; }
    
    /// <summary>Model name (default: grok-4-fast-non-reasoning)</summary>
    public string Model { get; set; } = XAIModels.Grok4FastNonReasoning;
    
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
    
    /// <summary>Seed for deterministic outputs</summary>
    public int? Seed { get; set; }
    
    /// <summary>HTTP timeout</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
}

