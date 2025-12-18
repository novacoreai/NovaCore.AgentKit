namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Represents a summarization checkpoint in a conversation.
/// Checkpoints allow efficient history management by summarizing older messages.
/// </summary>
public class ConversationCheckpoint
{
    /// <summary>Turn number up to which this checkpoint summarizes (inclusive)</summary>
    public required int UpToTurnNumber { get; init; }
    
    /// <summary>Summary text of the conversation up to this point</summary>
    public required string Summary { get; init; }
    
    /// <summary>When the checkpoint was created</summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>Optional metadata about the checkpoint</summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

