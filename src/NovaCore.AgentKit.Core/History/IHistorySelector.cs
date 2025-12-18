namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Selects which messages from full history should be sent to the model.
/// Handles tool result filtering, checkpoint summary injection, and multimodal content filtering.
/// </summary>
public interface IHistorySelector
{
    /// <summary>
    /// Select messages from full history, applying tool result filtering.
    /// </summary>
    /// <param name="fullHistory">Complete conversation history</param>
    /// <param name="toolResultConfig">Tool result filtering configuration</param>
    /// <returns>Filtered messages to send to the model</returns>
    List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        ToolResultConfig toolResultConfig);
    
    /// <summary>
    /// Select messages for context, incorporating a checkpoint summary if provided.
    /// If a checkpoint is provided, messages up to the checkpoint are replaced with
    /// the checkpoint summary, and only messages after the checkpoint are included.
    /// </summary>
    /// <param name="fullHistory">Complete conversation history</param>
    /// <param name="checkpoint">Optional checkpoint with conversation summary</param>
    /// <param name="toolResultConfig">Tool result filtering configuration</param>
    /// <returns>Filtered messages to send to the model, with checkpoint summary if applicable</returns>
    List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        ConversationCheckpoint? checkpoint,
        ToolResultConfig toolResultConfig);
    
    /// <summary>
    /// Select messages for context with full configuration options.
    /// </summary>
    /// <param name="fullHistory">Complete conversation history</param>
    /// <param name="checkpoint">Optional checkpoint with conversation summary</param>
    /// <param name="toolResultConfig">Tool result filtering configuration</param>
    /// <param name="maxMultimodalMessages">Maximum number of messages with multimodal content to retain (null = no limit)</param>
    /// <returns>Filtered messages to send to the model</returns>
    List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        ConversationCheckpoint? checkpoint,
        ToolResultConfig toolResultConfig,
        int? maxMultimodalMessages);
}
