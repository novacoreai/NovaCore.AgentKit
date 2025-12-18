namespace NovaCore.AgentKit.EntityFramework.Models;

/// <summary>
/// Represents a chat conversation session
/// </summary>
public class ChatSession
{
    /// <summary>Primary key</summary>
    public Guid Id { get; set; }
    
    /// <summary>Conversation identifier (unique)</summary>
    public string ConversationId { get; set; } = null!;
    
    /// <summary>Tenant ID for multi-tenancy</summary>
    public string? TenantId { get; set; }
    
    /// <summary>User ID</summary>
    public string? UserId { get; set; }
    
    /// <summary>When the session was created</summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>Last activity timestamp</summary>
    public DateTime LastActivityAt { get; set; }
    
    /// <summary>Metadata (JSON)</summary>
    public string? Metadata { get; set; }
    
    /// <summary>Whether the session is active</summary>
    public bool IsActive { get; set; }
    
    /// <summary>Navigation property for turns</summary>
    public List<ChatTurn> Turns { get; set; } = new();
    
    /// <summary>Navigation property for checkpoints</summary>
    public List<ConversationCheckpoint> Checkpoints { get; set; } = new();
}

