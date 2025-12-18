namespace NovaCore.AgentKit.Providers.Google;

/// <summary>
/// Configuration options for Google Gemini provider
/// </summary>
public class GoogleOptions
{
    /// <summary>Google AI API key (for Google AI Studio)</summary>
    public string? ApiKey { get; set; }
    
    /// <summary>Use Vertex AI instead of Google AI</summary>
    public bool UseVertexAI { get; set; } = false;
    
    /// <summary>GCP Project ID (required for Vertex AI)</summary>
    public string? ProjectId { get; set; }
    
    /// <summary>GCP Location/Region (required for Vertex AI, e.g., "us-central1")</summary>
    public string? Location { get; set; }
    
    /// <summary>GCP Service Account JSON credentials (for Vertex AI)</summary>
    public string? CredentialsJson { get; set; }
    
    /// <summary>Model name (default: gemini-2.5-flash)</summary>
    public string Model { get; set; } = GoogleModels.Gemini25Flash;
    
    /// <summary>Maximum tokens to generate</summary>
    public int? MaxTokens { get; set; }
    
    /// <summary>Temperature (0.0 - 2.0)</summary>
    public double? Temperature { get; set; }
    
    /// <summary>Top P sampling</summary>
    public double? TopP { get; set; }
    
    /// <summary>Top K sampling</summary>
    public int? TopK { get; set; }
    
    /// <summary>Thinking level for models that support reasoning (e.g., gemini-3-flash-preview)</summary>
    public ThinkingLevel? ThinkingLevel { get; set; }
    
    // Google-specific features
    
    /// <summary>Safety level (e.g., "BLOCK_NONE", "BLOCK_MEDIUM_AND_ABOVE")</summary>
    public string? SafetyLevel { get; set; }
    
    /// <summary>Enable grounding with Google Search</summary>
    public bool EnableGrounding { get; set; } = false;
    
    /// <summary>Enable Computer Use tool (required for gemini-2.5-computer-use-preview model)</summary>
    public bool EnableComputerUse { get; set; } = false;
    
    /// <summary>Computer Use environment (default: BROWSER)</summary>
    public ComputerUseEnvironment ComputerUseEnvironment { get; set; } = ComputerUseEnvironment.Browser;
    
    /// <summary>Excluded predefined Computer Use functions (e.g., "drag_and_drop", "hover_at")</summary>
    public List<string>? ExcludedComputerUseFunctions { get; set; }
    
    /// <summary>HTTP timeout</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Computer Use environment types
/// </summary>
public enum ComputerUseEnvironment
{
    /// <summary>Browser environment (web automation)</summary>
    Browser,
    
    /// <summary>Desktop environment (desktop automation)</summary>
    Desktop
}

