namespace NovaCore.AgentKit.Core;

/// <summary>
/// Client for Model Context Protocol (MCP) servers
/// </summary>
public interface IMcpClient : IAsyncDisposable
{
    /// <summary>
    /// Connect to MCP server and initialize
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Discover available tools from MCP server
    /// </summary>
    Task<List<McpToolDefinition>> DiscoverToolsAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Call an MCP tool by name
    /// </summary>
    Task<McpToolResult> CallToolAsync(
        string toolName, 
        Dictionary<string, object?> arguments, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Get current connection status
    /// </summary>
    McpConnectionStatus GetStatus();
}

