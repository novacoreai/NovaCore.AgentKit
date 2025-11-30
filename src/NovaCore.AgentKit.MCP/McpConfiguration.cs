using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.MCP;

/// <summary>
/// Configuration for MCP client
/// </summary>
public class McpConfiguration : IMcpConfiguration
{
    /// <summary>Command to execute (e.g., "node", "python", "npx")</summary>
    public required string Command { get; init; }
    
    /// <summary>Command arguments</summary>
    public List<string> Arguments { get; init; } = new();
    
    /// <summary>Environment variables</summary>
    public Dictionary<string, string> Environment { get; init; } = new();
    
    /// <summary>
    /// Working directory for the MCP process. 
    /// If null, defaults to system temp directory.
    /// This is important for commands like npx that need a proper working directory.
    /// </summary>
    public string? WorkingDirectory { get; init; }
    
    /// <summary>
    /// Optional list of tool names to expose to the agent.
    /// If null or empty, all discovered tools are included.
    /// If specified, only tools with names in this list will be added to the agent.
    /// </summary>
    public List<string>? AllowedTools { get; init; }
}

