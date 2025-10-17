namespace NovaCore.AgentKit.Core;

/// <summary>
/// Configuration interface for MCP servers
/// </summary>
public interface IMcpConfiguration
{
    /// <summary>Command to execute (e.g., "node", "python", "npx")</summary>
    string Command { get; }
    
    /// <summary>Command arguments</summary>
    List<string> Arguments { get; }
    
    /// <summary>Environment variables</summary>
    Dictionary<string, string> Environment { get; }
    
    /// <summary>Working directory for the MCP process</summary>
    string? WorkingDirectory { get; }
}

