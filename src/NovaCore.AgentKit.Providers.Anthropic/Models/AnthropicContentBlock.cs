using System.Text.Json.Serialization;

namespace NovaCore.AgentKit.Providers.Anthropic.Models;

/// <summary>
/// Content block in Anthropic message
/// </summary>
public class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("input")]
    public object? Input { get; set; }
    
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; set; }
    
    [JsonPropertyName("content")]
    public object? Content { get; set; }
    
    [JsonPropertyName("is_error")]
    public bool? IsError { get; set; }
    
    [JsonPropertyName("source")]
    public object? Source { get; set; }
}

