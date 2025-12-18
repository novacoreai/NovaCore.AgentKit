using System.Text.Json;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Definition of a tool from an MCP server
/// </summary>
public class McpToolDefinition
{
    /// <summary>Tool name</summary>
    public required string Name { get; init; }
    
    /// <summary>Tool description</summary>
    public required string Description { get; init; }
    
    /// <summary>JSON Schema for input parameters</summary>
    public required JsonDocument InputSchema { get; init; }
}

