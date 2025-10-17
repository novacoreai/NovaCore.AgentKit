using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core.History;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Interactive conversational agent for chat applications
/// </summary>
public class ChatAgent : IAsyncDisposable
{
    private readonly Agent _agent;
    private readonly string _conversationId;
    private readonly IHistoryStore? _historyStore;
    private readonly List<IMcpClient> _mcpClients;
    private readonly ILogger? _logger;
    private readonly CheckpointConfig _checkpointConfig;
    private bool _disposed;
    
    // Track messages already persisted to avoid duplicate saves
    private int _persistedMessageCount = 0;
    
    // Track last checkpoint turn number for auto-checkpointing
    private int _lastCheckpointTurnNumber = 0;
    
    internal ChatAgent(
        Agent agent, 
        string conversationId, 
        IHistoryStore? historyStore = null,
        List<IMcpClient>? mcpClients = null,
        ILogger? logger = null,
        CheckpointConfig? checkpointConfig = null)
    {
        _agent = agent;
        _conversationId = conversationId;
        _historyStore = historyStore;
        _mcpClients = mcpClients ?? new List<IMcpClient>();
        _logger = logger;
        _checkpointConfig = checkpointConfig ?? new CheckpointConfig();
    }
    
    /// <summary>
    /// Initialize ChatAgent by loading existing conversation from storage
    /// </summary>
    internal async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_historyStore == null) return;
        
        var existingHistory = await _historyStore.LoadAsync(_conversationId, ct);
        if (existingHistory != null && existingHistory.Count > 0)
        {
            _agent.GetHistoryManager().ReplaceHistory(existingHistory);
            _persistedMessageCount = existingHistory.Count;
            
            _logger?.LogInformation(
                "Resumed conversation {Id} with {Count} existing messages",
                _conversationId, existingHistory.Count);
        }
        
        // Load latest checkpoint to track where we are
        var latestCheckpoint = await _historyStore.GetLatestCheckpointAsync(_conversationId, ct);
        if (latestCheckpoint != null)
        {
            _lastCheckpointTurnNumber = latestCheckpoint.UpToTurnNumber;
            
            _logger?.LogDebug(
                "Found existing checkpoint at turn {Turn}",
                _lastCheckpointTurnNumber);
        }
    }
    
    /// <summary>
    /// Send a message and get a response.
    /// Returns the assistant's response message (may include tool calls for UI interaction).
    /// </summary>
    public async Task<ChatMessage> SendAsync(ChatMessage message, CancellationToken ct = default)
    {
        // Add user/tool message to history
        _agent.GetHistoryManager().AddMessage(message);
        
        // Persist the incoming message
        if (_historyStore != null)
        {
            await _historyStore.AppendMessageAsync(_conversationId, message, ct);
            _persistedMessageCount++;
            
            _logger?.LogDebug(
                "Persisted incoming message (role: {Role}) for conversation {Id}",
                message.Role, _conversationId);
        }
        
        // Execute turn (may pause at UI tool)
        var turn = await _agent.ExecuteTurnAsync(
            message.Text ?? "", 
            null, // files handled in ChatMessage already
            ct);
        
        // Get the last assistant message from history (this is the response)
        var history = _agent.GetHistoryManager().GetHistory();
        var lastAssistantMessage = history.LastOrDefault(m => m.Role == ChatRole.Assistant);
        
        if (lastAssistantMessage == null)
        {
            // Shouldn't happen, but handle gracefully
            return new ChatMessage(ChatRole.Assistant, turn.Error ?? "No response generated");
        }
        
        // Persist new messages incrementally (assistant + any tool results)
        if (_historyStore != null)
        {
            var newMessages = history.Skip(_persistedMessageCount).ToList();
            
            if (newMessages.Count > 0)
            {
                await _historyStore.AppendMessagesAsync(_conversationId, newMessages, ct);
                _persistedMessageCount = history.Count;
                
                _logger?.LogDebug(
                    "Persisted {Count} new message(s) for conversation {Id}",
                    newMessages.Count, _conversationId);
            }
            
            // Check if we need to create an automatic checkpoint
            await CheckAndCreateCheckpointAsync(ct);
        }
        
        return lastAssistantMessage;
    }
    
    /// <summary>
    /// Convenience method: Send a text message and get a response.
    /// </summary>
    public async Task<ChatMessage> SendAsync(string text, CancellationToken ct = default)
    {
        return await SendAsync(new ChatMessage(ChatRole.User, text), ct);
    }
    
    /// <summary>
    /// Convenience method: Send a text message with file attachments and get a response.
    /// </summary>
    public async Task<ChatMessage> SendAsync(
        string text,
        List<FileAttachment> files,
        CancellationToken ct = default)
    {
        var contents = new List<IMessageContent> 
        { 
            new TextMessageContent(text) 
        };
        contents.AddRange(files.Select(f => f.ToMessageContent()));
        
        return await SendAsync(new ChatMessage(ChatRole.User, contents), ct);
    }
    
    /// <summary>
    /// Check if automatic checkpointing should be triggered and create checkpoint if needed
    /// </summary>
    private async Task CheckAndCreateCheckpointAsync(CancellationToken ct)
    {
        // Check if auto-checkpointing is enabled
        if (!_checkpointConfig.EnableAutoCheckpointing || 
            _checkpointConfig.SummarizationTool == null ||
            _historyStore == null)
        {
            return;
        }
        
        var currentHistory = _agent.GetHistoryManager().GetHistory();
        var messagesSinceLastCheckpoint = currentHistory.Count - _lastCheckpointTurnNumber;
        
        // Check if we've reached the threshold
        if (messagesSinceLastCheckpoint < _checkpointConfig.SummarizeEveryNMessages)
        {
            return;
        }
        
        _logger?.LogInformation(
            "Auto-checkpoint triggered: {MessageCount} messages since last checkpoint (threshold: {Threshold})",
            messagesSinceLastCheckpoint, _checkpointConfig.SummarizeEveryNMessages);
        
        try
        {
            // Get messages up to the checkpoint point (exclude the most recent N messages)
            var checkpointTurnNumber = currentHistory.Count - _checkpointConfig.KeepRecentMessages;
            if (checkpointTurnNumber <= _lastCheckpointTurnNumber)
            {
                // Not enough new messages to create a meaningful checkpoint
                return;
            }
            
            // Get the history segment to summarize (from last checkpoint to new checkpoint point)
            var rawMessagesToSummarize = currentHistory
                .Skip(_lastCheckpointTurnNumber)
                .Take(checkpointTurnNumber - _lastCheckpointTurnNumber)
                .ToList();
            
            if (rawMessagesToSummarize.Count == 0)
            {
                return;
            }
            
            // IMPORTANT: Apply tool result filtering before summarization
            // Respects HistoryRetentionConfig.ToolResults (e.g., max tool calls, drop all, etc.)
            var historySelector = _agent.GetHistorySelector();
            var retentionConfig = _agent.GetRetentionConfig();
            
            // Apply same filtering rules used for LLM context
            var filteredMessagesToSummarize = historySelector.SelectMessagesForContext(
                rawMessagesToSummarize,
                checkpoint: null, // Don't apply checkpoint logic here, just tool filtering
                retentionConfig);
            
            if (filteredMessagesToSummarize.Count == 0)
            {
                return;
            }
            
            _logger?.LogDebug(
                "Preparing {Original} messages for summarization (after filtering: {Filtered})",
                rawMessagesToSummarize.Count,
                filteredMessagesToSummarize.Count);
            
            // Serialize filtered messages for the summarization tool
            var historyJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                conversation_id = _conversationId,
                from_turn = _lastCheckpointTurnNumber,
                to_turn = checkpointTurnNumber,
                original_message_count = rawMessagesToSummarize.Count,
                filtered_message_count = filteredMessagesToSummarize.Count,
                messages = filteredMessagesToSummarize.Select(m => new
                {
                    role = m.Role.ToString(),
                    text = m.Text,
                    has_tool_calls = m.ToolCalls?.Any() ?? false,
                    is_tool_result = m.Role == ChatRole.Tool
                })
            });
            
            // Call the summarization tool
            _logger?.LogDebug("Calling summarization tool for {Count} messages", filteredMessagesToSummarize.Count);
            var summaryJson = await _checkpointConfig.SummarizationTool.InvokeAsync(historyJson, ct);
            
            // Extract summary from result (expect { "summary": "..." } or just the text)
            string summary;
            try
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(summaryJson);
                if (result.TryGetProperty("summary", out var summaryElement))
                {
                    summary = summaryElement.GetString() ?? summaryJson;
                }
                else
                {
                    summary = summaryJson;
                }
            }
            catch
            {
                // If not JSON, treat as plain text summary
                summary = summaryJson;
            }
            
            // Create the checkpoint
            var checkpoint = new History.ConversationCheckpoint
            {
                UpToTurnNumber = checkpointTurnNumber,
                Summary = summary,
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    { "auto_created", true },
                    { "original_message_count", rawMessagesToSummarize.Count },
                    { "filtered_message_count", filteredMessagesToSummarize.Count },
                    { "kept_recent_messages", _checkpointConfig.KeepRecentMessages }
                }
            };
            
            await _historyStore.CreateCheckpointAsync(_conversationId, checkpoint, ct);
            _lastCheckpointTurnNumber = checkpointTurnNumber;
            
            _logger?.LogInformation(
                "Created automatic checkpoint at turn {Turn} (summarized {Filtered}/{Original} messages after tool filtering, keeping {Recent} recent)",
                checkpointTurnNumber, filteredMessagesToSummarize.Count, rawMessagesToSummarize.Count, _checkpointConfig.KeepRecentMessages);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create automatic checkpoint");
            // Don't throw - checkpoint failure shouldn't break the conversation
        }
    }
    
    /// <summary>
    /// Get conversation history statistics
    /// </summary>
    public HistoryStats GetStats()
    {
        return _agent.GetHistoryManager().GetStats();
    }
    
    /// <summary>
    /// Clear conversation history
    /// </summary>
    public void ClearHistory()
    {
        _agent.GetHistoryManager().Clear();
    }
    
    /// <summary>
    /// Get the conversation ID
    /// </summary>
    public string ConversationId => _conversationId;
    
    /// <summary>
    /// Create a checkpoint (summary point) in the conversation.
    /// This allows efficient history management by summarizing older messages.
    /// </summary>
    /// <param name="summary">Summary of the conversation up to this point</param>
    /// <param name="upToTurnNumber">Turn number to summarize up to (uses current turn if null)</param>
    /// <param name="ct">Cancellation token</param>
    public async Task CreateCheckpointAsync(
        string summary, 
        int? upToTurnNumber = null,
        CancellationToken ct = default)
    {
        if (_historyStore == null)
        {
            throw new InvalidOperationException(
                "History store must be configured to create checkpoints");
        }
        
        var currentHistory = _agent.GetHistoryManager().GetHistory();
        var turnNumber = upToTurnNumber ?? currentHistory.Count - 1;
        
        var checkpoint = new ConversationCheckpoint
        {
            UpToTurnNumber = turnNumber,
            Summary = summary,
            CreatedAt = DateTime.UtcNow
        };
        
        await _historyStore.CreateCheckpointAsync(_conversationId, checkpoint, ct);
        
        _logger?.LogInformation(
            "Created checkpoint for conversation {Id} at turn {Turn}",
            _conversationId, turnNumber);
    }
    
    /// <summary>
    /// Get the most recent checkpoint for this conversation
    /// </summary>
    public async Task<ConversationCheckpoint?> GetLatestCheckpointAsync(CancellationToken ct = default)
    {
        if (_historyStore == null)
        {
            return null;
        }
        
        return await _historyStore.GetLatestCheckpointAsync(_conversationId, ct);
    }
    
    /// <summary>
    /// Dispose of MCP clients and resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        // Dispose all MCP clients
        foreach (var mcpClient in _mcpClients)
        {
            try
            {
                await mcpClient.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

