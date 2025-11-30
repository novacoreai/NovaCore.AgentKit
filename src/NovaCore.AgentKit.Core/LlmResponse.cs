namespace NovaCore.AgentKit.Core;

/// <summary>
/// Response from LLM (non-streaming)
/// </summary>
public class LlmResponse
{
    /// <summary>Generated text</summary>
    public string? Text { get; init; }
    
    /// <summary>Tool calls requested by the model</summary>
    public List<LlmToolCall>? ToolCalls { get; init; }
    
    /// <summary>Finish reason</summary>
    public LlmFinishReason? FinishReason { get; init; }
    
    /// <summary>Token usage</summary>
    public LlmUsage? Usage { get; init; }
}

/// <summary>
/// Streaming update from LLM
/// </summary>
public class LlmStreamingUpdate
{
    /// <summary>Text delta</summary>
    public string? TextDelta { get; init; }
    
    /// <summary>Tool call (may be partial during streaming)</summary>
    public LlmToolCall? ToolCall { get; init; }
    
    /// <summary>Finish reason (set on final update)</summary>
    public LlmFinishReason? FinishReason { get; init; }
    
    /// <summary>Token usage (typically set on final update)</summary>
    public LlmUsage? Usage { get; init; }
}

/// <summary>
/// Tool call from LLM
/// </summary>
public class LlmToolCall
{
    /// <summary>Unique ID for this tool call</summary>
    public required string Id { get; init; }
    
    /// <summary>Tool name</summary>
    public required string Name { get; init; }
    
    /// <summary>Arguments as JSON string</summary>
    public required string ArgumentsJson { get; init; }
}

/// <summary>
/// Why the LLM stopped generating
/// </summary>
public enum LlmFinishReason
{
    Stop,           // Natural stop
    Length,         // Max tokens reached
    ToolCalls,      // Stopped to execute tools
    ContentFilter   // Content policy violation
}

/// <summary>
/// Token usage information
/// </summary>
public class LlmUsage
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens;
    
    /// <summary>Cost for input tokens (in USD)</summary>
    public decimal InputCost { get; init; }
    
    /// <summary>Cost for output tokens (in USD)</summary>
    public decimal OutputCost { get; init; }
    
    /// <summary>Total cost (in USD)</summary>
    public decimal TotalCost => InputCost + OutputCost;
}

