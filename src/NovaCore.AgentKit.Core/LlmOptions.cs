using System.Text.Json;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Options for LLM requests (replaces ChatOptions)
/// </summary>
public class LlmOptions
{
    /// <summary>Maximum tokens to generate</summary>
    public int? MaxTokens { get; set; }
    
    /// <summary>Temperature (0.0-2.0 typically)</summary>
    public double? Temperature { get; set; }
    
    /// <summary>Top P sampling</summary>
    public double? TopP { get; set; }
    
    /// <summary>Stop sequences</summary>
    public List<string>? StopSequences { get; set; }
    
    /// <summary>Available tools with their JSON schemas</summary>
    public Dictionary<string, LlmTool>? Tools { get; set; }
    
    /// <summary>Additional provider-specific options</summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}

/// <summary>
/// Tool definition for LLM
/// </summary>
public class LlmTool
{
    /// <summary>Tool name</summary>
    public required string Name { get; init; }
    
    /// <summary>Tool description</summary>
    public required string Description { get; init; }
    
    /// <summary>JSON schema for parameters</summary>
    public required JsonElement ParameterSchema { get; init; }
}

