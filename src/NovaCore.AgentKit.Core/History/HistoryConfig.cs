namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Configuration for history management
/// </summary>
public class HistoryConfig
{
    /// <summary>Compress history after this many messages</summary>
    public int CompressThreshold { get; set; } = 50;
    
    /// <summary>Keep last N tool results after compression</summary>
    public int KeepToolResults { get; set; } = 5;
    
    /// <summary>Keep last N messages after compression</summary>
    public int KeepRecentMessages { get; set; } = 10;
    
    /// <summary>Log when messages are truncated</summary>
    public bool LogTruncation { get; set; } = true;
    
    /// <summary>Tool result storage strategy</summary>
    public ToolResultStorageStrategy ToolResultStrategy { get; set; } = 
        ToolResultStorageStrategy.KeepLastN;
}

/// <summary>
/// Strategy for handling tool results in history
/// </summary>
public enum ToolResultStorageStrategy
{
    /// <summary>Keep all tool results (can cause context overflow)</summary>
    KeepAll,
    
    /// <summary>Keep last N tool results</summary>
    KeepLastN,
    
    /// <summary>Summarize old tool results</summary>
    Summarize,
    
    /// <summary>Keep only successful tool results</summary>
    KeepSuccessful
}

