namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Statistics about conversation history
/// </summary>
public class HistoryStats
{
    /// <summary>Total number of messages</summary>
    public int TotalMessages { get; init; }
    
    /// <summary>Number of user messages</summary>
    public int UserMessages { get; init; }
    
    /// <summary>Number of assistant messages</summary>
    public int AssistantMessages { get; init; }
    
    /// <summary>Number of tool messages</summary>
    public int ToolMessages { get; init; }
    
    /// <summary>Estimated token count</summary>
    public int EstimatedTokens { get; init; }
    
    /// <summary>Number of times history has been compressed</summary>
    public int CompressionCount { get; init; }
}

