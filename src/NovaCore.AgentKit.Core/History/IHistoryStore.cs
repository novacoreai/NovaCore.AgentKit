namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Persistent storage for conversation history
/// </summary>
public interface IHistoryStore
{
    /// <summary>
    /// Append a single message to the conversation (incremental save)
    /// </summary>
    Task AppendMessageAsync(string conversationId, ChatMessage message, CancellationToken ct = default);
    
    /// <summary>
    /// Append multiple messages to the conversation (batch incremental save)
    /// </summary>
    Task AppendMessagesAsync(string conversationId, List<ChatMessage> messages, CancellationToken ct = default);
    
    /// <summary>
    /// Save conversation history (LEGACY: for backward compatibility, prefer AppendMessageAsync for new code)
    /// </summary>
    Task SaveAsync(string conversationId, List<ChatMessage> history, CancellationToken ct = default);
    
    /// <summary>
    /// Load conversation history
    /// </summary>
    Task<List<ChatMessage>?> LoadAsync(string conversationId, CancellationToken ct = default);
    
    /// <summary>
    /// Delete conversation history
    /// </summary>
    Task DeleteAsync(string conversationId, CancellationToken ct = default);
    
    /// <summary>
    /// List all conversation IDs
    /// </summary>
    Task<List<string>> ListConversationsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get the number of messages currently stored for a conversation
    /// </summary>
    Task<int> GetMessageCountAsync(string conversationId, CancellationToken ct = default);
    
    /// <summary>
    /// Create a checkpoint (summary point) in the conversation
    /// </summary>
    Task CreateCheckpointAsync(
        string conversationId, 
        ConversationCheckpoint checkpoint, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Get the most recent checkpoint for a conversation
    /// </summary>
    Task<ConversationCheckpoint?> GetLatestCheckpointAsync(
        string conversationId, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Load conversation history from the latest checkpoint forward (or all if no checkpoint exists)
    /// </summary>
    Task<(ConversationCheckpoint? checkpoint, List<ChatMessage> messages)> LoadFromCheckpointAsync(
        string conversationId, 
        CancellationToken ct = default);
}

