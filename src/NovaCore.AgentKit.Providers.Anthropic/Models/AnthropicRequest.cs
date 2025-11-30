using System.Text.Json.Serialization;

namespace NovaCore.AgentKit.Providers.Anthropic.Models;

/// <summary>
/// Request model for Anthropic Messages API
/// </summary>
public class AnthropicRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; set; }
    
    [JsonPropertyName("messages")]
    public required List<AnthropicMessage> Messages { get; set; }
    
    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; set; }
    
    [JsonPropertyName("system")]
    public string? System { get; set; }
    
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }
    
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }
    
    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }
    
    [JsonPropertyName("stop_sequences")]
    public List<string>? StopSequences { get; set; }
    
    [JsonPropertyName("tools")]
    public List<AnthropicTool>? Tools { get; set; }
    
    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }
    
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}


