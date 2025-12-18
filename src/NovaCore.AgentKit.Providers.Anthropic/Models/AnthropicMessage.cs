using System.Text.Json.Serialization;

namespace NovaCore.AgentKit.Providers.Anthropic.Models;

/// <summary>
/// Message in Anthropic format
/// </summary>
public class AnthropicMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }
    
    [JsonPropertyName("content")]
    public required object Content { get; set; } // Can be string or List<AnthropicContentBlock>
}


