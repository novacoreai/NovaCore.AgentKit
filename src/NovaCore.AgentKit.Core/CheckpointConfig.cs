namespace NovaCore.AgentKit.Core;

/// <summary>
/// Configuration for automatic conversation checkpointing/summarization
/// </summary>
public class CheckpointConfig
{
    /// <summary>
    /// Enable automatic checkpointing.
    /// When enabled, the system will automatically create checkpoints at regular intervals.
    /// Default: false
    /// </summary>
    public bool EnableAutoCheckpointing { get; set; } = false;
    
    /// <summary>
    /// Number of messages before triggering automatic summarization.
    /// Example: If set to 50, a checkpoint is created every 50 messages.
    /// Default: 50
    /// </summary>
    public int SummarizeEveryNMessages { get; set; } = 50;
    
    /// <summary>
    /// Number of recent (uncompressed) messages to keep after the checkpoint.
    /// These messages remain in full detail when sent to the LLM.
    /// Example: If set to 10, the last 10 messages are always sent in full,
    /// while older messages are replaced with the checkpoint summary.
    /// Default: 10
    /// </summary>
    public int KeepRecentMessages { get; set; } = 10;
    
    /// <summary>
    /// Tool to use for generating checkpoint summaries.
    /// This tool should accept the conversation history and return a summary string.
    /// The host application provides this tool (e.g., calling an LLM for summarization).
    /// If null, auto-checkpointing will be disabled even if EnableAutoCheckpointing is true.
    /// </summary>
    public ITool? SummarizationTool { get; set; }
}

