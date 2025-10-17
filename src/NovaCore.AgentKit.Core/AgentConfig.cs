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
    
    /// <summary>History management configuration</summary>
    public HistoryConfig History { get; set; } = new();
    
    /// <summary>
    /// History retention configuration - controls what gets sent to the model.
    /// Note: Full history is still stored, this only affects LLM context.
    /// </summary>
    public HistoryRetentionConfig HistoryRetention { get; set; } = new();
    
    /// <summary>Output sanitization options</summary>
    public SanitizationOptions Sanitization { get; set; } = new();
    
    /// <summary>Enable automatic turn validation</summary>
    public bool EnableTurnValidation { get; set; } = true;
    
    /// <summary>Enable output sanitization</summary>
    public bool EnableOutputSanitization { get; set; } = true;
    
    /// <summary>Logging configuration for agent turns</summary>
    public AgentLoggingConfig Logging { get; set; } = new();
    
    /// <summary>Automatic checkpoint/summarization configuration</summary>
    public CheckpointConfig Checkpointing { get; set; } = new();
}

