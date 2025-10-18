namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Configuration for automatic conversation summarization (ChatAgent).
/// When enabled, older messages are summarized into checkpoints to maintain context while reducing memory usage.
/// 
/// <para><b>How it works:</b></para>
/// <list type="number">
/// <item>Conversation grows to TriggerAt messages (e.g., 100)</item>
/// <item>Calculate: MessagesToSummarize = TriggerAt - KeepRecent (e.g., 100 - 10 = 90)</item>
/// <item>Summarize first 90 messages → Create checkpoint</item>
/// <item>Remove those 90 messages from memory</item>
/// <item>Keep last 10 messages in memory</item>
/// <item>Database retains ALL 100 messages + checkpoint</item>
/// </list>
/// 
/// <para><b>Example:</b> TriggerAt = 100, KeepRecent = 10</para>
/// <list type="bullet">
/// <item>At 100 msgs: Summarize 1-90, keep 91-100 → Memory: 10 msgs + 1 checkpoint</item>
/// <item>At 110 msgs: Summarize 91-100, keep 101-110 → Memory: 10 msgs + 2 checkpoints</item>
/// <item>Database always has: ALL messages + ALL checkpoints</item>
/// </list>
/// </summary>
public class SummarizationConfig
{
    /// <summary>
    /// Enable automatic summarization.
    /// When enabled and SummarizationTool is provided, older messages are automatically
    /// summarized into checkpoints to maintain context while reducing memory usage.
    /// Default: false
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Trigger summarization when history reaches this many messages.
    /// 
    /// <para><b>How it works:</b></para>
    /// <para>At TriggerAt messages, the first (TriggerAt - KeepRecent) messages are summarized.</para>
    /// 
    /// <para><b>Example:</b> TriggerAt = 100, KeepRecent = 10</para>
    /// <para>→ Summarize first 90 messages when conversation reaches 100</para>
    /// 
    /// Default: 100
    /// </summary>
    public int TriggerAt { get; set; } = 100;
    
    /// <summary>
    /// Keep this many recent messages in memory after summarization.
    /// 
    /// <para><b>Calculation:</b></para>
    /// <para>MessagesToSummarize = TriggerAt - KeepRecent</para>
    /// 
    /// <para><b>Example:</b> TriggerAt = 100, KeepRecent = 10</para>
    /// <list type="bullet">
    /// <item>At 100 messages: Summarize first 90, keep last 10 in memory</item>
    /// <item>Result: 10 messages in memory, 90 in checkpoint summary</item>
    /// <item>Database: Still has all 100 messages</item>
    /// </list>
    /// 
    /// Default: 10
    /// </summary>
    public int KeepRecent { get; set; } = 10;
    
    /// <summary>
    /// Tool to use for generating conversation summaries.
    /// If null, summarization is disabled even if Enabled is true.
    /// </summary>
    public ITool? SummarizationTool { get; set; }
    
    /// <summary>
    /// Tool result filtering configuration.
    /// Controls how verbose tool outputs are handled in context.
    /// Uses placeholder "[Omitted]" approach to maintain conversation structure.
    /// </summary>
    public ToolResultConfig ToolResults { get; set; } = new();
    
    /// <summary>
    /// Validates the configuration and returns any issues.
    /// </summary>
    public List<string> Validate()
    {
        var issues = new List<string>();
        
        if (KeepRecent >= TriggerAt)
        {
            issues.Add($"KeepRecent ({KeepRecent}) must be less than TriggerAt ({TriggerAt}). " +
                      $"At least 1 message must be summarized. Recommended: KeepRecent = 10% of TriggerAt.");
        }
        
        if (KeepRecent < 5)
        {
            issues.Add($"KeepRecent ({KeepRecent}) is very low. Recommended minimum: 5 messages.");
        }
        
        if (Enabled && SummarizationTool == null)
        {
            issues.Add("Summarization is enabled but SummarizationTool is null. Provide a tool or disable summarization.");
        }
        
        return issues;
    }
    
    /// <summary>
    /// Gets a human-readable summary of the configuration.
    /// </summary>
    public string GetSummary()
    {
        if (!Enabled)
        {
            return "disabled";
        }
        
        var messagesToSummarize = TriggerAt - KeepRecent;
        return $"trigger at {TriggerAt} msgs (summarize first {messagesToSummarize}, keep {KeepRecent})";
    }
}

