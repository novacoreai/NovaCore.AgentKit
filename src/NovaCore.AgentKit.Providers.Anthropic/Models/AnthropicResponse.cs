using System.Text.Json.Serialization;

namespace NovaCore.AgentKit.Providers.Anthropic.Models;

/// <summary>
/// Response from Anthropic Messages API
/// </summary>
public class AnthropicResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "message";
    
    [JsonPropertyName("role")]
    public string Role { get; set; } = "assistant";
    
    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; set; } = new();
    
    [JsonPropertyName("model")]
    public required string Model { get; set; }
    
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
    
    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }
    
    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
}

/// <summary>
/// Token usage information
/// </summary>
public class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }
    
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
    
    [JsonPropertyName("cache_creation_input_tokens")]
    public int? CacheCreationInputTokens { get; set; }
    
    [JsonPropertyName("cache_read_input_tokens")]
    public int? CacheReadInputTokens { get; set; }
}


