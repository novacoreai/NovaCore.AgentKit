using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.Core.Sanitization;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Configuration for agent behavior
/// </summary>
public class AgentConfig
{
    /// <summary>Maximum tool call rounds per turn (safety limit)</summary>
    public int MaxToolRoundsPerTurn { get; set; } = 10;
    
    /// <summary>System prompt for the agent</summary>
    public string? SystemPrompt { get; set; }
    
    /// <summary>
    /// Automatic summarization configuration for ChatAgents.
    /// When enabled, older messages are summarized into checkpoints to maintain context
    /// while reducing memory usage. Database retains all messages.
    /// </summary>
    public SummarizationConfig Summarization { get; set; } = new();
    
    /// <summary>
    /// Tool result filtering configuration.
    /// Controls how verbose tool outputs are handled by replacing filtered results
    /// with "[Omitted]" placeholders. Useful for browser agents and ReAct agents.
    /// </summary>
    public ToolResultConfig ToolResults { get; set; } = new();
    
    /// <summary>
    /// Maximum number of messages with multimodal content (images, audio) to retain
    /// in the history sent to the model. When set, only the last N multimodal messages
    /// are included in API calls. Older multimodal content is stripped from messages
    /// but the text content is preserved.
    /// 
    /// Default: null (no limit - all multimodal content is retained).
    /// Set to 1 for computer use scenarios where only the most recent screenshot matters.
    /// </summary>
    public int? MaxMultimodalMessages { get; set; }
    
    /// <summary>Output sanitization options</summary>
    public SanitizationOptions Sanitization { get; set; } = new();
    
    /// <summary>Enable automatic turn validation</summary>
    public bool EnableTurnValidation { get; set; } = true;
    
    /// <summary>Enable output sanitization</summary>
    public bool EnableOutputSanitization { get; set; } = true;
    
    /// <summary>
    /// [OBSOLETE] Use Summarization property instead.
    /// This property is kept for backward compatibility and maps to Summarization.
    /// </summary>
    [Obsolete("Use Summarization property instead. This will be removed in a future version.")]
    public CheckpointConfig Checkpointing 
    { 
        get => new CheckpointConfig
        {
            EnableAutoCheckpointing = Summarization.Enabled,
            SummarizeEveryNMessages = Summarization.TriggerAt,
            KeepRecentMessages = Summarization.KeepRecent,
            SummarizationTool = Summarization.SummarizationTool
        };
        set
        {
            if (value != null)
            {
                Summarization.Enabled = value.EnableAutoCheckpointing;
                Summarization.TriggerAt = value.SummarizeEveryNMessages;
                Summarization.KeepRecent = value.KeepRecentMessages;
                Summarization.SummarizationTool = value.SummarizationTool;
            }
        }
    }
}

