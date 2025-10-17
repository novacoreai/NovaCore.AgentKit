namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Manages conversation history
/// </summary>
public interface IHistoryManager
{
    /// <summary>
    /// Add a message to history
    /// </summary>
    void AddMessage(ChatMessage message);
    
    /// <summary>
    /// Get current conversation history
    /// </summary>
    List<ChatMessage> GetHistory();
    
    /// <summary>
    /// Replace entire history
    /// </summary>
    void ReplaceHistory(List<ChatMessage> history);
    
    /// <summary>
    /// Compress history to reduce token count
    /// </summary>
    void CompressHistory();
    
    /// <summary>
    /// Clear all history
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Get history statistics
    /// </summary>
    HistoryStats GetStats();
}

