namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Configuration for how much conversation history to retain when sending context to the model.
/// Note: Full history is still stored in DB/memory - this only affects what's sent to the LLM.
/// 
/// <para><b>How These Settings Work Together:</b></para>
/// <list type="number">
/// <item>First, tool results are filtered (based on ToolResults config)</item>
/// <item>Then, messages are limited to MaxMessagesToSend</item>
/// <item>Within that limit, the most recent KeepRecentMessagesIntact are never trimmed</item>
/// <item>Finally, conversation structure is validated and repaired if needed</item>
/// </list>
/// 
/// <para><b>Example:</b> MaxMessagesToSend = 20, KeepRecentMessagesIntact = 10</para>
/// <list type="bullet">
/// <item>Last 10 messages always included (protected)</item>
/// <item>Additional 10 messages selected from older history</item>
/// <item>Total = 20 messages sent to LLM</item>
/// </list>
/// </summary>
public class HistoryRetentionConfig
{
    /// <summary>
    /// Maximum total messages (across entire conversation) to send to the model.
    /// This is the TOTAL limit after all filtering.
    /// Set to 0 for unlimited.
    /// Default: 50
    /// 
    /// <para><b>Tip:</b> For ReAct agents with many tool calls, set this higher (100+) or use tool result filtering instead.</para>
    /// </summary>
    public int MaxMessagesToSend { get; set; } = 50;
    
    /// <summary>
    /// Always keep the most recent N messages intact (never subject to trimming).
    /// These messages are ALWAYS included even if it means exceeding limits elsewhere.
    /// This ensures the latest context is always available.
    /// Default: 5
    /// 
    /// <para><b>Important:</b> Should be significantly less than MaxMessagesToSend.</para>
    /// <para><b>Recommended:</b> Set to 20-30% of MaxMessagesToSend.</para>
    /// <para><b>Example:</b> If MaxMessagesToSend = 50, set this to 10-15.</para>
    /// </summary>
    public int KeepRecentMessagesIntact { get; set; } = 5;
    
    /// <summary>
    /// Always include the system message in context, even if it exceeds MaxMessagesToSend.
    /// Default: true
    /// </summary>
    public bool AlwaysIncludeSystemMessage { get; set; } = true;
    
    /// <summary>
    /// Configuration for handling tool result messages in history.
    /// </summary>
    public ToolResultRetentionConfig ToolResults { get; set; } = new();
    
    /// <summary>
    /// If true and a checkpoint exists, use checkpoint summary instead of older messages.
    /// This significantly reduces context size for long conversations.
    /// Default: true
    /// </summary>
    public bool UseCheckpointSummary { get; set; } = true;
    
    /// <summary>
    /// Validates the configuration and returns any warnings or errors.
    /// </summary>
    /// <returns>List of validation messages (empty if valid)</returns>
    public List<string> Validate()
    {
        var issues = new List<string>();
        
        if (MaxMessagesToSend > 0 && KeepRecentMessagesIntact >= MaxMessagesToSend)
        {
            issues.Add($"KeepRecentMessagesIntact ({KeepRecentMessagesIntact}) should be less than MaxMessagesToSend ({MaxMessagesToSend}). " +
                      $"Recommended: Set KeepRecentMessagesIntact to 20-30% of MaxMessagesToSend.");
        }
        
        if (MaxMessagesToSend > 0 && KeepRecentMessagesIntact > MaxMessagesToSend * 0.5)
        {
            issues.Add($"KeepRecentMessagesIntact ({KeepRecentMessagesIntact}) is more than 50% of MaxMessagesToSend ({MaxMessagesToSend}). " +
                      $"This leaves little room for older context. Consider lowering to {(int)(MaxMessagesToSend * 0.3)}.");
        }
        
        if (ToolResults.MaxToolResults > 0 && MaxMessagesToSend > 0 && ToolResults.MaxToolResults > MaxMessagesToSend)
        {
            issues.Add($"MaxToolResults ({ToolResults.MaxToolResults}) exceeds MaxMessagesToSend ({MaxMessagesToSend}). " +
                      $"Tool result limit will never be reached.");
        }
        
        return issues;
    }
    
    /// <summary>
    /// Gets a summary of the effective configuration settings.
    /// Useful for logging and debugging.
    /// </summary>
    public string GetSummary()
    {
        var parts = new List<string>();
        
        if (MaxMessagesToSend == 0)
        {
            parts.Add("unlimited messages");
        }
        else
        {
            parts.Add($"max {MaxMessagesToSend} messages");
            parts.Add($"{KeepRecentMessagesIntact} recent protected");
        }
        
        if (ToolResults.MaxToolResults > 0)
        {
            parts.Add($"max {ToolResults.MaxToolResults} tool results ({ToolResults.Strategy})");
        }
        else if (ToolResults.Strategy != ToolResultStrategy.KeepRecent)
        {
            parts.Add($"tool results: {ToolResults.Strategy}");
        }
        
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Configuration for retaining tool call results in conversation context.
/// 
/// <para><b>Use Case:</b> Browser agents and automation agents generate very verbose tool outputs.
/// Use this to dramatically reduce context size while keeping the conversation intact.</para>
/// 
/// <para><b>Important:</b> When tool results are filtered, the corresponding Assistant messages
/// that made those tool calls are also removed to maintain conversation coherence.</para>
/// </summary>
public class ToolResultRetentionConfig
{
    /// <summary>
    /// Maximum number of tool result messages to include in context.
    /// Useful for agents with many tool calls (e.g., browser automation).
    /// Set to 0 for unlimited.
    /// Default: 0 (unlimited)
    /// 
    /// <para><b>Recommended for Browser Agents:</b> 3-5 results</para>
    /// <para><b>Recommended for ReAct Agents:</b> 5-10 results</para>
    /// </summary>
    public int MaxToolResults { get; set; } = 0;
    
    /// <summary>
    /// Strategy for which tool results to keep when limit is reached.
    /// Default: KeepRecent
    /// </summary>
    public ToolResultStrategy Strategy { get; set; } = ToolResultStrategy.KeepRecent;
}

/// <summary>
/// Strategy for selecting which tool results to retain in context
/// </summary>
public enum ToolResultStrategy
{
    /// <summary>Keep all tool results (subject to MaxToolResults limit)</summary>
    KeepRecent,
    
    /// <summary>Keep only successful tool results (no errors)</summary>
    KeepSuccessful,
    
    /// <summary>Keep only the most recent tool result (useful for browser agents)</summary>
    KeepOne,
    
    /// <summary>Drop all tool results from context (extreme context reduction)</summary>
    DropAll
}

