namespace NovaCore.AgentKit.Providers.Groq;

/// <summary>
/// Configuration options for Groq provider.
/// Groq API is OpenAI-compatible and uses base URL: https://api.groq.com/openai/v1
/// For full API documentation, see: https://console.groq.com/docs/api-reference
/// </summary>
public class GroqOptions
{
    /// <summary>Groq API key (required)</summary>
    public required string ApiKey { get; set; }
    
    /// <summary>Model name (default: qwen/qwen3-32b)</summary>
    public string Model { get; set; } = GroqModels.Qwen3_32B;
    
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

