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
    private readonly IAgentObserver? _observer;
    private readonly SummarizationConfig _summarizationConfig;
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
        IAgentObserver? observer = null,
        SummarizationConfig? summarizationConfig = null)
    {
        _agent = agent;
        _conversationId = conversationId;
        _historyStore = historyStore;
        _mcpClients = mcpClients ?? new List<IMcpClient>();
        _observer = observer;
        _summarizationConfig = summarizationConfig ?? new SummarizationConfig();
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
        }
        
        // Load latest checkpoint to track where we are
        var latestCheckpoint = await _historyStore.GetLatestCheckpointAsync(_conversationId, ct);
        if (latestCheckpoint != null)
        {
            _lastCheckpointTurnNumber = latestCheckpoint.UpToTurnNumber;
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
        // Check if auto-summarization is enabled
        if (!_summarizationConfig.Enabled || 
            _summarizationConfig.SummarizationTool == null ||
            _historyStore == null)
        {
            return;
        }
        
        var currentHistory = _agent.GetHistoryManager().GetHistory();
        
        // Check if we've reached the trigger threshold
        if (currentHistory.Count < _summarizationConfig.TriggerAt)
        {
            return;
        }
        
        // Calculate how many messages to summarize
        // Formula: TriggerAt - KeepRecent = Messages to summarize
        var messagesToSummarize = _summarizationConfig.TriggerAt - _summarizationConfig.KeepRecent;
        
        try
        {
            // Checkpoint turn number = how many messages we're summarizing
            var checkpointTurnNumber = messagesToSummarize;
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
            // Uses the same tool filtering config as LLM context
            var historySelector = _agent.GetHistorySelector();
            var toolResultConfig = _summarizationConfig.ToolResults;
            
            // Apply same filtering rules used for LLM context
            var filteredMessagesToSummarize = historySelector.SelectMessagesForContext(
                rawMessagesToSummarize,
                checkpoint: null, // Don't apply checkpoint logic here, just tool filtering
                toolResultConfig);
            
            if (filteredMessagesToSummarize.Count == 0)
            {
                return;
            }
            
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
            var summaryJson = await _summarizationConfig.SummarizationTool.InvokeAsync(historyJson, ct);
            
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
                    { "kept_recent_messages", _summarizationConfig.KeepRecent }
                }
            };
            
            await _historyStore.CreateCheckpointAsync(_conversationId, checkpoint, ct);
            _lastCheckpointTurnNumber = checkpointTurnNumber;
            
            // NOW: Remove the summarized messages from in-memory history
            // Keep only the recent messages (after the checkpoint)
            var recentMessages = currentHistory.Skip(messagesToSummarize).ToList();
            _agent.GetHistoryManager().ReplaceHistory(recentMessages);
        }
        catch (Exception)
        {
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

