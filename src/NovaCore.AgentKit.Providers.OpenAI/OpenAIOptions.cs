namespace NovaCore.AgentKit.Providers.OpenAI;

/// <summary>
/// Configuration options for OpenAI provider
/// </summary>
public class OpenAIOptions
{
    /// <summary>OpenAI API key (required)</summary>
    public required string ApiKey { get; set; }
    
    /// <summary>Model name (default: gpt-4o)</summary>
    public string Model { get; set; } = OpenAIModels.GPT4o;
    
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
    
    // OpenAI-specific features
    
    /// <summary>Response format (e.g., "json_object" for JSON mode)</summary>
    public string? ResponseFormat { get; set; }
    
    /// <summary>Enable structured outputs</summary>
    public bool UseStructuredOutputs { get; set; } = false;
    
    /// <summary>Seed for deterministic outputs</summary>
    public int? Seed { get; set; }
    
    /// <summary>Reasoning effort level for GPT-5.1 models (none, low, medium, high)</summary>
    public string? ReasoningEffort { get; set; }
    
    /// <summary>Prompt cache retention duration for extended caching (e.g., "24h")</summary>
    public string? PromptCacheRetention { get; set; }
    
    /// <summary>Organization ID (optional)</summary>
    public string? OrganizationId { get; set; }
    
    /// <summary>API base URL (optional, for Azure OpenAI or custom endpoints)</summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>HTTP timeout</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
}

