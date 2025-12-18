namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// In-memory implementation of history manager.
/// Simplified to just store messages - no auto-compression.
/// Compression is handled via checkpoint/summarization at the Agent level.
/// </summary>
public class InMemoryHistoryManager : IHistoryManager
{
    private readonly List<ChatMessage> _history = new();
    
    public InMemoryHistoryManager()
    {
    }
    
    public void AddMessage(ChatMessage message)
    {
        _history.Add(message);
    }
    
    public List<ChatMessage> GetHistory() => _history.ToList();
    
    public void ReplaceHistory(List<ChatMessage> history)
    {
        _history.Clear();
        _history.AddRange(history);
    }
    
    public void Clear() => _history.Clear();
    
    public HistoryStats GetStats()
    {
        return new HistoryStats
        {
            TotalMessages = _history.Count,
            UserMessages = _history.Count(m => m.Role == ChatRole.User),
            AssistantMessages = _history.Count(m => m.Role == ChatRole.Assistant),
            ToolMessages = _history.Count(m => m.Role == ChatRole.Tool),
            EstimatedTokens = EstimateTokens(_history),
            CompressionCount = 0 // No longer used
        };
    }
    
    private static int EstimateTokens(List<ChatMessage> messages)
    {
        // Rough estimate: 1 token ≈ 4 characters
        var totalChars = messages.Sum(m => (m.Text?.Length ?? 0) + 50); // +50 for metadata
        return totalChars / 4;
    }
}
