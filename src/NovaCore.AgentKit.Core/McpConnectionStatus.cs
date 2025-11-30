namespace NovaCore.AgentKit.Core;

/// <summary>
/// Connection status of MCP client
/// </summary>
public enum McpConnectionStatus
{
    /// <summary>Not connected</summary>
    Disconnected,
    
    /// <summary>Connecting in progress</summary>
    Connecting,
    
    /// <summary>Connected and ready</summary>
    Connected,
    
    /// <summary>Error state</summary>
    Error
}

