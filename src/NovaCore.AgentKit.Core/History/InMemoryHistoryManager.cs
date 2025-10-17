using Microsoft.Extensions.Logging;

namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// In-memory implementation of history manager
/// </summary>
public class InMemoryHistoryManager : IHistoryManager
{
    private readonly List<ChatMessage> _history = new();
    private readonly HistoryConfig _config;
    private readonly ILogger? _logger;
    private int _compressionCount = 0;
    
    public InMemoryHistoryManager(HistoryConfig config, ILogger? logger = null)
    {
        _config = config;
        _logger = logger;
    }
    
    public void AddMessage(ChatMessage message)
    {
        _history.Add(message);
        
        // Auto-compress if threshold reached
        if (_history.Count > _config.CompressThreshold)
        {
            CompressHistory();
        }
    }
    
    public List<ChatMessage> GetHistory() => _history.ToList();
    
    public void ReplaceHistory(List<ChatMessage> history)
    {
        _history.Clear();
        _history.AddRange(history);
    }
    
    public void CompressHistory()
    {
        var originalCount = _history.Count;
        
        // Keep system message
        var system = _history.FirstOrDefault(m => m.Role == ChatRole.System);
        
        // Apply tool result strategy
        var toolMessages = ApplyToolResultStrategy(_history);
        
        // Keep recent messages
        var recentMessages = _history
            .TakeLast(_config.KeepRecentMessages)
            .ToList();
        
        // Rebuild history
        _history.Clear();
        if (system != null) _history.Add(system);
        _history.AddRange(toolMessages);
        
        // Add recent messages that aren't already included
        foreach (var msg in recentMessages)
        {
            if (!_history.Contains(msg))
            {
                _history.Add(msg);
            }
        }
        
        _compressionCount++;
        var removedCount = originalCount - _history.Count;
        
        if (_config.LogTruncation && removedCount > 0)
        {
            _logger?.LogInformation(
                "History compressed #{Count}: {Removed} messages removed, {Kept} kept",
                _compressionCount, removedCount, _history.Count);
        }
    }
    
    private List<ChatMessage> ApplyToolResultStrategy(List<ChatMessage> history)
    {
        var toolMessages = history.Where(m => m.Role == ChatRole.Tool).ToList();
        
        return _config.ToolResultStrategy switch
        {
            ToolResultStorageStrategy.KeepAll => toolMessages,
            
            ToolResultStorageStrategy.KeepLastN => 
                toolMessages.TakeLast(_config.KeepToolResults).ToList(),
            
            ToolResultStorageStrategy.KeepSuccessful => 
                toolMessages
                    .Where(m => m.Text != null && !m.Text.Contains("error", StringComparison.OrdinalIgnoreCase))
                    .TakeLast(_config.KeepToolResults)
                    .ToList(),
            
            ToolResultStorageStrategy.Summarize => 
                SummarizeToolResults(toolMessages),
            
            _ => toolMessages.TakeLast(_config.KeepToolResults).ToList()
        };
    }
    
    private List<ChatMessage> SummarizeToolResults(List<ChatMessage> toolMessages)
    {
        // Keep recent tool results as-is
        var recentTools = toolMessages.TakeLast(_config.KeepToolResults).ToList();
        
        // Summarize older tool results
        var oldTools = toolMessages.Take(toolMessages.Count - _config.KeepToolResults).ToList();
        
        if (oldTools.Any())
        {
            var summary = $"[{oldTools.Count} previous tool calls summarized]";
            var summaryMessage = new ChatMessage(
                ChatRole.Tool,
                summary,
                "summarized");
            
            return new List<ChatMessage> { summaryMessage }.Concat(recentTools).ToList();
        }
        
        return recentTools;
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
            CompressionCount = _compressionCount
        };
    }
    
    private static int EstimateTokens(List<ChatMessage> messages)
    {
        // Rough estimate: 1 token ≈ 4 characters
        var totalChars = messages.Sum(m => (m.Text?.Length ?? 0) + 50); // +50 for metadata
        return totalChars / 4;
    }
}

