namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Selects which messages from full history should be sent to the model
/// </summary>
public interface IHistorySelector
{
    /// <summary>
    /// Select messages from full history based on retention configuration.
    /// This controls what gets sent to the LLM while preserving full history in storage.
    /// </summary>
    /// <param name="fullHistory">Complete conversation history</param>
    /// <param name="config">Retention configuration</param>
    /// <returns>Filtered messages to send to the model</returns>
    List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        HistoryRetentionConfig config);
    
    /// <summary>
    /// Select messages for context, optionally incorporating a checkpoint summary.
    /// If a checkpoint is provided and config.UseCheckpointSummary is true,
    /// the checkpoint summary replaces older messages.
    /// </summary>
    /// <param name="fullHistory">Complete conversation history</param>
    /// <param name="checkpoint">Optional checkpoint with conversation summary</param>
    /// <param name="config">Retention configuration</param>
    /// <returns>Filtered messages to send to the model, optionally with checkpoint summary</returns>
    List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        ConversationCheckpoint? checkpoint,
        HistoryRetentionConfig config);
}

