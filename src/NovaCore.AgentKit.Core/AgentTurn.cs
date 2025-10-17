namespace NovaCore.AgentKit.Core;

/// <summary>
/// Result of a single agent turn
/// </summary>
public class AgentTurn
{
    /// <summary>Agent's response text</summary>
    public required string Response { get; init; }
    
    /// <summary>Number of tool call rounds executed</summary>
    public int ToolCallsExecuted { get; init; }
    
    /// <summary>Completion signal (if complete_task tool was called)</summary>
    public string? CompletionSignal { get; init; }
    
    /// <summary>Whether the turn completed successfully</summary>
    public bool Success { get; init; } = true;
    
    /// <summary>Error message (if any)</summary>
    public string? Error { get; init; }
}

