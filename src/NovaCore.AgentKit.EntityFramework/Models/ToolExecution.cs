namespace NovaCore.AgentKit.EntityFramework.Models;

/// <summary>
/// Represents a tool execution within a turn
/// </summary>
public class ToolExecution
{
    /// <summary>Primary key</summary>
    public Guid Id { get; set; }
    
    /// <summary>Foreign key to turn</summary>
    public Guid TurnId { get; set; }
    
    /// <summary>Tool name</summary>
    public string ToolName { get; set; } = null!;
    
    /// <summary>Tool arguments (JSON)</summary>
    public string Arguments { get; set; } = null!;
    
    /// <summary>Tool result (JSON)</summary>
    public string? Result { get; set; }
    
    /// <summary>Whether execution was successful</summary>
    public bool Success { get; set; }
    
    /// <summary>Error message (if failed)</summary>
    public string? Error { get; set; }
    
    /// <summary>When the tool was executed</summary>
    public DateTime ExecutedAt { get; set; }
    
    /// <summary>Execution duration in milliseconds</summary>
    public int DurationMs { get; set; }
    
    /// <summary>Navigation property for turn</summary>
    public ChatTurn Turn { get; set; } = null!;
}

