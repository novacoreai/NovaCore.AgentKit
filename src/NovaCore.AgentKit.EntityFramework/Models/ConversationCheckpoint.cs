namespace NovaCore.AgentKit.EntityFramework.Models;

/// <summary>
/// Represents a summarization checkpoint in a conversation.
/// Checkpoints allow efficient history management by summarizing older messages.
/// </summary>
public class ConversationCheckpoint
{
    /// <summary>Primary key</summary>
    public Guid Id { get; set; }
    
    /// <summary>Foreign key to session</summary>
    public Guid SessionId { get; set; }
    
    /// <summary>Turn number up to which this checkpoint summarizes (inclusive)</summary>
    public int UpToTurnNumber { get; set; }
    
    /// <summary>Summary text of the conversation up to this point</summary>
    public string Summary { get; set; } = null!;
    
    /// <summary>When the checkpoint was created</summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>Optional metadata (JSON) about the checkpoint</summary>
    public string? Metadata { get; set; }
    
    /// <summary>Navigation property for session</summary>
    public ChatSession Session { get; set; } = null!;
}

