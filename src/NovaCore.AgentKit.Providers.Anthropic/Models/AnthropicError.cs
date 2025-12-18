using System.Text.Json.Serialization;

namespace NovaCore.AgentKit.Providers.Anthropic.Models;

/// <summary>
/// Error response from Anthropic API
/// </summary>
public class AnthropicErrorResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "error";
    
    [JsonPropertyName("error")]
    public required AnthropicError Error { get; set; }
}

/// <summary>
/// Error details
/// </summary>
public class AnthropicError
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    
    [JsonPropertyName("message")]
    public required string Message { get; set; }
}


