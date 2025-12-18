using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.EntityFramework.Models;

/// <summary>
/// Represents a single turn in a conversation
/// </summary>
public class ChatTurn
{
    /// <summary>Primary key</summary>
    public Guid Id { get; set; }
    
    /// <summary>Foreign key to session</summary>
    public Guid SessionId { get; set; }
    
    /// <summary>Turn number (sequence)</summary>
    public int TurnNumber { get; set; }
    
    /// <summary>Role of the message sender</summary>
    public ChatRole Role { get; set; }
    
    /// <summary>Message content (text)</summary>
    public string Content { get; set; } = null!;
    
    /// <summary>
    /// JSON-serialized multimodal content (images, files, etc.)
    /// Null if message is text-only
    /// </summary>
    public string? ContentJson { get; set; }
    
    /// <summary>When the turn was created</summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>Tool call ID (for tool result messages)</summary>
    public string? ToolCallId { get; set; }
    
    /// <summary>
    /// JSON-serialized tool calls made by the assistant
    /// Null if message doesn't contain tool calls
    /// </summary>
    public string? ToolCallsJson { get; set; }
    
    /// <summary>Navigation property for session</summary>
    public ChatSession Session { get; set; } = null!;
    
    /// <summary>Navigation property for tool executions</summary>
    public List<ToolExecution> ToolExecutions { get; set; } = new();
}

