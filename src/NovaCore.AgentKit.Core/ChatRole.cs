namespace NovaCore.AgentKit.Core;

/// <summary>
/// Represents the role of a message in a conversation
/// </summary>
public enum ChatRole
{
    /// <summary>System instructions</summary>
    System = 0,
    
    /// <summary>User messages</summary>
    User = 1,
    
    /// <summary>Assistant responses</summary>
    Assistant = 2,
    
    /// <summary>Tool results (separate rail)</summary>
    Tool = 3
}

