namespace NovaCore.AgentKit.Core;

/// <summary>
/// Result of a single agent turn
/// </summary>
public class AgentTurn
{
    /// <summary>Agent's response text</summary>
    public required string Response { get; init; }
    
    /// <summary>Number of LLM calls (rounds) executed in this turn</summary>
    public int LlmCallsExecuted { get; init; }
    
    /// <summary>Completion signal (if complete_task tool was called)</summary>
    public string? CompletionSignal { get; init; }
    
    /// <summary>Whether the turn completed successfully</summary>
    public bool Success { get; init; } = true;
    
    /// <summary>Error message (if any)</summary>
    public string? Error { get; init; }
    
    /// <summary>Total input tokens used in this turn</summary>
    public int TotalInputTokens { get; init; }
    
    /// <summary>Total output tokens used in this turn</summary>
    public int TotalOutputTokens { get; init; }
    
    /// <summary>Total cost for this turn (in USD)</summary>
    public decimal TotalCost { get; init; }
}

