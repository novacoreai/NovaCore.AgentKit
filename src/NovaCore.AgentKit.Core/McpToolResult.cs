using System.Text.Json;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Result of MCP tool execution
/// </summary>
public class McpToolResult
{
    /// <summary>Whether execution was successful</summary>
    public bool Success { get; init; }
    
    /// <summary>Result data</summary>
    public JsonElement? Data { get; init; }
    
    /// <summary>Error message if failed</summary>
    public string? Error { get; init; }
}

