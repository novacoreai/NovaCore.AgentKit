using NovaCore.AgentKit.Core.TurnValidation;

namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Simplified history selector that applies tool result filtering and checkpoint summaries.
/// </summary>
public class SmartHistorySelector : IHistorySelector
{
    private readonly ITurnValidator _turnValidator;
    
    public SmartHistorySelector()
    {
        _turnValidator = new TurnValidator();
    }
    
    public List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        ToolResultConfig toolResultConfig)
    {
        return SelectMessagesForContext(fullHistory, checkpoint: null, toolResultConfig, maxMultimodalMessages: null);
    }
    
    public List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        ConversationCheckpoint? checkpoint,
        ToolResultConfig toolResultConfig)
    {
        return SelectMessagesForContext(fullHistory, checkpoint, toolResultConfig, maxMultimodalMessages: null);
    }
    
    public List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        ConversationCheckpoint? checkpoint,
        ToolResultConfig toolResultConfig,
        int? maxMultimodalMessages)
    {
        if (fullHistory.Count == 0)
        {
            return new List<ChatMessage>();
        }
        
        // Separate system messages
        var systemMessages = fullHistory.Where(m => m.Role == ChatRole.System).ToList();
        var conversationMessages = fullHistory.Where(m => m.Role != ChatRole.System).ToList();
        
        // If checkpoint is provided, inject checkpoint summary
        if (checkpoint != null)
        {
            // Create a system message with the checkpoint summary
            var checkpointMessage = new ChatMessage(
                ChatRole.System, 
                $"[Conversation summary up to turn {checkpoint.UpToTurnNumber}]: {checkpoint.Summary}");
            
            // Only include messages after the checkpoint
            conversationMessages = conversationMessages
                .Skip(checkpoint.UpToTurnNumber + 1)
                .ToList();
            
            // Add checkpoint summary as a system message
            systemMessages.Add(checkpointMessage);
        }
        
        // Apply tool result filtering (placeholder-based approach)
        var filteredMessages = FilterToolResults(conversationMessages, toolResultConfig);
        
        // Apply multimodal content filtering if configured
        if (maxMultimodalMessages.HasValue)
        {
            filteredMessages = FilterMultimodalContent(filteredMessages, maxMultimodalMessages.Value);
        }
        
        // Combine system messages with filtered conversation
        var result = new List<ChatMessage>(systemMessages);
        result.AddRange(filteredMessages);
        
        // Simple validation for edge cases where messages were actually removed (checkpoint skip)
        result = EnsureValidStart(result);
        
        return result;
    }
    
    /// <summary>
    /// Filters tool results by replacing content with placeholders.
    /// This preserves conversation structure and avoids the need for complex repair logic.
    /// </summary>
    private List<ChatMessage> FilterToolResults(
        List<ChatMessage> messages,
        ToolResultConfig toolConfig)
    {
        // No filtering? Return as-is
        if (toolConfig.KeepRecent == 0)
        {
            return messages;
        }
        
        // Get all tool results
        var toolMessages = messages.Where(m => m.Role == ChatRole.Tool).ToList();
        
        // No tool messages? Return as-is
        if (toolMessages.Count == 0)
        {
            return messages;
        }
        
        // If we have fewer tool results than the limit, keep all
        if (toolMessages.Count <= toolConfig.KeepRecent)
        {
            return messages;
        }
        
        // Determine which tool results to keep with full content (most recent N)
        var toolResultsToKeep = toolMessages.TakeLast(toolConfig.KeepRecent).ToList();
        
        // Create a HashSet of tool call IDs to keep (with full content)
        var keepFullContentIds = new HashSet<string?>(
            toolResultsToKeep.Select(m => m.ToolCallId));
        
        // Replace tool results not in the "keep" set with placeholders
        var result = new List<ChatMessage>();
        int replacedCount = 0;
        
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.Tool)
            {
                if (keepFullContentIds.Contains(msg.ToolCallId))
                {
                    // Keep full content
                    result.Add(msg);
                }
                else
                {
                    // Replace with placeholder to maintain structure
                    result.Add(new ChatMessage(ChatRole.Tool, "[Omitted]", msg.ToolCallId));
                    replacedCount++;
                }
            }
            else
            {
                // All other messages (User, Assistant, System) are kept as-is
                // This is KEY: We never remove messages anymore
                result.Add(msg);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Filters multimodal content by keeping only the last N messages with images/audio.
    /// Older multimodal content is stripped but text content is preserved.
    /// </summary>
    private List<ChatMessage> FilterMultimodalContent(
        List<ChatMessage> messages,
        int maxMultimodalMessages)
    {
        if (maxMultimodalMessages <= 0)
        {
            // Remove all multimodal content
            return messages.Select(StripMultimodalContent).ToList();
        }
        
        // Find all messages that have multimodal content
        var multimodalIndices = new List<int>();
        for (int i = 0; i < messages.Count; i++)
        {
            if (HasMultimodalContent(messages[i]))
            {
                multimodalIndices.Add(i);
            }
        }
        
        // If we have fewer multimodal messages than the limit, keep all
        if (multimodalIndices.Count <= maxMultimodalMessages)
        {
            return messages;
        }
        
        // Determine which multimodal messages to keep (most recent N)
        var indicesToKeep = new HashSet<int>(
            multimodalIndices.TakeLast(maxMultimodalMessages));
        
        // Process messages: strip multimodal content from those not in keep set
        var result = new List<ChatMessage>();
        for (int i = 0; i < messages.Count; i++)
        {
            if (HasMultimodalContent(messages[i]) && !indicesToKeep.Contains(i))
            {
                // Strip multimodal content but keep text
                result.Add(StripMultimodalContent(messages[i]));
            }
            else
            {
                result.Add(messages[i]);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Check if a message contains multimodal content (images, audio, etc.)
    /// </summary>
    private bool HasMultimodalContent(ChatMessage message)
    {
        if (message.Contents == null || message.Contents.Count == 0)
        {
            return false;
        }
        
        return message.Contents.Any(c => c is ImageMessageContent);
    }
    
    /// <summary>
    /// Strip multimodal content from a message, keeping text and tool-related content.
    /// This preserves the message structure for tool calls/results while removing images.
    /// </summary>
    private ChatMessage StripMultimodalContent(ChatMessage message)
    {
        if (message.Contents == null || message.Contents.Count == 0)
        {
            return message;
        }
        
        // Keep everything EXCEPT ImageMessageContent (and other future multimodal types)
        // This preserves TextMessageContent, ToolCallMessageContent, and ToolResultMessageContent
        var nonImageContents = message.Contents
            .Where(c => c is not ImageMessageContent)
            .ToList();
        
        if (nonImageContents.Count == 0)
        {
            // Only had image content - create a placeholder message
            return new ChatMessage(message.Role, "[Image omitted]", message.ToolCallId);
        }
        
        if (nonImageContents.Count == message.Contents.Count)
        {
            // No multimodal content was present
            return message;
        }
        
        // Create new message with non-image content (preserves tool calls/results)
        return new ChatMessage(message.Role, nonImageContents, message.ToolCallId);
    }
    
    /// <summary>
    /// Ensures the conversation starts properly after checkpoint skip.
    /// With the placeholder approach for tool results, we don't break structure during filtering,
    /// so this is only needed when checkpoint skips messages.
    /// </summary>
    private List<ChatMessage> EnsureValidStart(List<ChatMessage> messages)
    {
        if (messages.Count == 0)
        {
            return messages;
        }
        
        // Find first non-system message
        var firstNonSystem = messages.FirstOrDefault(m => m.Role != ChatRole.System);
        
        if (firstNonSystem == null)
        {
            // Only system messages - this is fine
            return messages;
        }
        
        // Ensure conversation starts with User (after system messages)
        if (firstNonSystem.Role != ChatRole.User)
        {
            var systemMessages = messages.TakeWhile(m => m.Role == ChatRole.System).ToList();
            var conversationMessages = messages.SkipWhile(m => m.Role == ChatRole.System).ToList();
            
            var result = new List<ChatMessage>(systemMessages);
            result.Add(new ChatMessage(ChatRole.User, "[Previous context summarized]"));
            result.AddRange(conversationMessages);
            
            return result;
        }
        
        return messages;
    }
}
