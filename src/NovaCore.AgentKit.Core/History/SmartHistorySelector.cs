using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core.TurnValidation;

namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Intelligent history selector that applies retention rules
/// </summary>
public class SmartHistorySelector : IHistorySelector
{
    private readonly ILogger? _logger;
    private readonly ITurnValidator _turnValidator;
    
    public SmartHistorySelector(ILogger? logger = null)
    {
        _logger = logger;
        _turnValidator = new TurnValidator();
    }
    
    public List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        HistoryRetentionConfig config)
    {
        return SelectMessagesForContext(fullHistory, checkpoint: null, config);
    }
    
    public List<ChatMessage> SelectMessagesForContext(
        List<ChatMessage> fullHistory,
        ConversationCheckpoint? checkpoint,
        HistoryRetentionConfig config)
    {
        if (fullHistory.Count == 0)
        {
            return new List<ChatMessage>();
        }
        
        // Separate system messages
        var systemMessages = fullHistory.Where(m => m.Role == ChatRole.System).ToList();
        var conversationMessages = fullHistory.Where(m => m.Role != ChatRole.System).ToList();
        
        // If checkpoint is provided and enabled, inject checkpoint summary
        if (checkpoint != null && config.UseCheckpointSummary)
        {
            _logger?.LogDebug(
                "Using checkpoint summary for messages up to turn {Turn}",
                checkpoint.UpToTurnNumber);
            
            // Create a system message with the checkpoint summary
            var checkpointMessage = new ChatMessage(
                ChatRole.System, 
                $"[Previous conversation summary up to message {checkpoint.UpToTurnNumber}]: {checkpoint.Summary}");
            
            // Only include messages after the checkpoint
            conversationMessages = conversationMessages
                .Skip(checkpoint.UpToTurnNumber + 1)
                .ToList();
            
            // Add checkpoint summary as a system message
            systemMessages.Add(checkpointMessage);
        }
        
        // Apply tool result filtering
        var filteredMessages = FilterToolResults(conversationMessages, config.ToolResults);
        
        // Apply message limit
        var limitedMessages = ApplyMessageLimit(filteredMessages, config);
        
        // Combine system messages (if configured) with filtered conversation
        var result = new List<ChatMessage>();
        
        if (config.AlwaysIncludeSystemMessage || checkpoint != null)
        {
            result.AddRange(systemMessages);
        }
        
        result.AddRange(limitedMessages);
        
        _logger?.LogDebug(
            "History selection: {FullCount} → {SelectedCount} messages (System: {SystemCount}, Conversation: {ConvCount}, Checkpoint: {HasCheckpoint})",
            fullHistory.Count,
            result.Count,
            systemMessages.Count,
            limitedMessages.Count,
            checkpoint != null);
        
        // Validate and repair conversation structure after filtering
        result = EnsureValidConversation(result);
        
        return result;
    }
    
    private List<ChatMessage> FilterToolResults(
        List<ChatMessage> messages,
        ToolResultRetentionConfig toolConfig)
    {
        // Handle DropAll strategy first
        if (toolConfig.Strategy == ToolResultStrategy.DropAll)
        {
            _logger?.LogDebug("Dropping all tool result messages from context");
            return messages.Where(m => m.Role != ChatRole.Tool).ToList();
        }
        
        // Separate tool results from other messages
        var toolMessages = messages.Where(m => m.Role == ChatRole.Tool).ToList();
        
        // No tool messages? Return as-is
        if (toolMessages.Count == 0)
        {
            return messages;
        }
        
        // If default strategy (KeepRecent) and no limit, return all
        if (toolConfig.Strategy == ToolResultStrategy.KeepRecent && toolConfig.MaxToolResults == 0)
        {
            return messages;
        }
        
        // Apply strategy
        var filteredToolMessages = toolConfig.Strategy switch
        {
            ToolResultStrategy.KeepOne => toolMessages.TakeLast(1).ToList(),
            ToolResultStrategy.KeepSuccessful => FilterSuccessfulToolResults(toolMessages, toolConfig.MaxToolResults),
            ToolResultStrategy.KeepRecent => ApplyToolResultLimit(toolMessages, toolConfig.MaxToolResults),
            _ => toolMessages
        };
        
        // Create a HashSet of tool call IDs for fast lookup
        var includedToolCallIds = new HashSet<string?>(
            filteredToolMessages.Select(m => m.ToolCallId));
        
        // Reconstruct message list maintaining chronological order
        // IMPORTANT: Also filter out assistant messages with orphaned tool calls
        var result = new List<ChatMessage>();
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.Tool)
            {
                // Only include tool results that are in our filtered set
                if (includedToolCallIds.Contains(msg.ToolCallId))
                {
                    result.Add(msg);
                }
            }
            else if (msg.Role == ChatRole.Assistant && msg.ToolCalls?.Any() == true)
            {
                // Only include assistant messages with tool calls if ALL their tool calls have results
                var allToolCallsHaveResults = msg.ToolCalls.All(tc => includedToolCallIds.Contains(tc.Id));
                
                if (allToolCallsHaveResults)
                {
                    result.Add(msg);
                }
                else
                {
                    // Drop this assistant message - its tool calls were filtered out
                    _logger?.LogDebug(
                        "Dropping assistant message with orphaned tool calls (tool results were filtered)");
                }
            }
            else
            {
                // User messages, system messages, assistant without tool calls - always include
                result.Add(msg);
            }
        }
        
        _logger?.LogDebug(
            "Tool result filtering: {Original} → {Filtered} tool messages (Strategy: {Strategy})",
            toolMessages.Count,
            filteredToolMessages.Count,
            toolConfig.Strategy);
        
        return result;
    }
    
    private List<ChatMessage> FilterSuccessfulToolResults(List<ChatMessage> toolMessages, int maxToolResults)
    {
        // Filter out tool results that contain error indicators
        var successfulResults = toolMessages.Where(msg =>
        {
            var text = msg.Text?.ToLowerInvariant() ?? "";
            return !text.Contains("error") && 
                   !text.Contains("\"success\":false") &&
                   !text.Contains("\"success\": false");
        }).ToList();
        
        // Apply limit if specified
        return ApplyToolResultLimit(successfulResults, maxToolResults);
    }
    
    private List<ChatMessage> ApplyToolResultLimit(List<ChatMessage> toolMessages, int maxToolResults)
    {
        if (maxToolResults == 0 || toolMessages.Count <= maxToolResults)
        {
            return toolMessages;
        }
        
        // Keep the most recent N tool results
        return toolMessages.TakeLast(maxToolResults).ToList();
    }
    
    private List<ChatMessage> ApplyMessageLimit(
        List<ChatMessage> messages,
        HistoryRetentionConfig config)
    {
        // No limit? Return all
        if (config.MaxMessagesToSend == 0 || messages.Count <= config.MaxMessagesToSend)
        {
            return messages;
        }
        
        var keepRecent = config.KeepRecentMessagesIntact;
        var totalToKeep = config.MaxMessagesToSend;
        
        // If we want to keep more recent messages than our total limit, adjust
        if (keepRecent >= totalToKeep)
        {
            _logger?.LogWarning(
                "KeepRecentMessagesIntact ({KeepRecent}) >= MaxMessagesToSend ({MaxMessages}), keeping last {MaxMessages} messages",
                keepRecent,
                totalToKeep,
                totalToKeep);
            return messages.TakeLast(totalToKeep).ToList();
        }
        
        // Split into recent (must keep) and older (can trim)
        var recentMessages = messages.TakeLast(keepRecent).ToList();
        var olderMessages = messages.Take(messages.Count - keepRecent).ToList();
        
        // Calculate how many older messages we can keep
        var olderToKeep = totalToKeep - keepRecent;
        
        if (olderToKeep <= 0)
        {
            // Only keep recent messages
            _logger?.LogDebug(
                "Keeping only {KeepRecent} recent messages (trimmed {Trimmed} older messages)",
                keepRecent,
                olderMessages.Count);
            return recentMessages;
        }
        
        // Take the most recent from older messages
        var selectedOlder = olderMessages.TakeLast(olderToKeep).ToList();
        
        _logger?.LogDebug(
            "Message limit applied: {Original} → {Final} messages (Older: {Older}, Recent: {Recent})",
            messages.Count,
            selectedOlder.Count + recentMessages.Count,
            selectedOlder.Count,
            recentMessages.Count);
        
        // Combine older + recent
        var result = new List<ChatMessage>(selectedOlder);
        result.AddRange(recentMessages);
        return result;
    }
    
    /// <summary>
    /// Ensures filtered conversation maintains valid User/Assistant alternation.
    /// Fixes issues caused by filtering that can create consecutive Assistant messages.
    /// </summary>
    private List<ChatMessage> EnsureValidConversation(List<ChatMessage> messages)
    {
        if (messages.Count == 0)
        {
            return messages;
        }
        
        // Validate the conversation
        var validation = _turnValidator.Validate(messages);
        
        if (validation.IsValid)
        {
            return messages;
        }
        
        // Conversation is invalid after filtering - repair it
        _logger?.LogWarning(
            "Filtered history created invalid conversation structure. Applying repair. Errors: {Errors}",
            string.Join(", ", validation.Errors));
        
        // CRITICAL FIX: Track which Assistant messages we keep (by their tool call IDs)
        // This allows us to drop orphaned Tool results
        var keptToolCallIds = new HashSet<string>();
        var repaired = new List<ChatMessage>();
        ChatRole? lastNonToolRole = null;
        
        // First pass: Determine which Assistant messages to keep and collect their tool call IDs
        var messagesToKeep = new HashSet<ChatMessage>();
        lastNonToolRole = null;
        
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.System)
            {
                messagesToKeep.Add(msg);
            }
            else if (msg.Role == ChatRole.User)
            {
                // May need placeholder before User
                if (lastNonToolRole == ChatRole.User)
                {
                    // Will insert placeholder in second pass
                }
                messagesToKeep.Add(msg);
                lastNonToolRole = ChatRole.User;
            }
            else if (msg.Role == ChatRole.Assistant)
            {
                if (lastNonToolRole == ChatRole.Assistant)
                {
                    // Skip this Assistant - it creates consecutive Assistants
                    _logger?.LogDebug("Dropping Assistant message to fix alternation (tool calls: {HasToolCalls})", 
                        msg.ToolCalls?.Any() ?? false);
                    continue;
                }
                messagesToKeep.Add(msg);
                lastNonToolRole = ChatRole.Assistant;
                
                // Track tool call IDs from kept Assistant messages
                if (msg.ToolCalls != null)
                {
                    foreach (var tc in msg.ToolCalls)
                    {
                        keptToolCallIds.Add(tc.Id);
                    }
                }
            }
        }
        
        // Second pass: Build repaired list, only including Tool results for kept Assistants
        lastNonToolRole = null;
        
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.System)
            {
                repaired.Add(msg);
            }
            else if (msg.Role == ChatRole.Tool)
            {
                // CRITICAL: Only add Tool result if its corresponding Assistant was kept
                if (msg.ToolCallId != null && keptToolCallIds.Contains(msg.ToolCallId))
                {
                    repaired.Add(msg);
                }
                else
                {
                    _logger?.LogDebug("Dropping orphaned Tool result (id: {ToolCallId})", msg.ToolCallId);
                }
            }
            else if (msg.Role == ChatRole.User)
            {
                if (lastNonToolRole == ChatRole.User)
                {
                    // Two User messages in a row - insert placeholder
                    repaired.Add(new ChatMessage(ChatRole.Assistant, "[Context summarized]"));
                    _logger?.LogDebug("Inserted placeholder Assistant between consecutive User messages");
                }
                repaired.Add(msg);
                lastNonToolRole = ChatRole.User;
            }
            else if (msg.Role == ChatRole.Assistant && messagesToKeep.Contains(msg))
            {
                repaired.Add(msg);
                lastNonToolRole = ChatRole.Assistant;
            }
        }
        
        // Validate the repaired conversation
        var repairedValidation = _turnValidator.Validate(repaired);
        
        if (!repairedValidation.IsValid)
        {
            _logger?.LogWarning(
                "Conversation repair did not fully resolve issues. Remaining errors: {Errors}",
                string.Join(", ", repairedValidation.Errors));
        }
        else
        {
            _logger?.LogInformation(
                "Conversation structure repaired successfully. {Original} → {Repaired} messages",
                messages.Count,
                repaired.Count);
        }
        
        return repaired;
    }
}

