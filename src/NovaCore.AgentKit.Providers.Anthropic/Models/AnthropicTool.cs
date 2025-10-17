using System.Text.Json.Serialization;

namespace NovaCore.AgentKit.Providers.Anthropic.Models;

/// <summary>
/// Tool definition for Anthropic API
/// </summary>
public class AnthropicTool
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("input_schema")]
    public required AnthropicInputSchema InputSchema { get; set; }
}

/// <summary>
/// Input schema for tool parameters
/// </summary>
public class AnthropicInputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";
    
    [JsonPropertyName("properties")]
    public Dictionary<string, object>? Properties { get; set; }
    
    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}


