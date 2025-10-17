namespace NovaCore.AgentKit.Core;

/// <summary>
/// Represents a tool call made by the assistant
/// </summary>
public class ToolCall
{
    /// <summary>
    /// Unique identifier for this tool call
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Name of the function/tool to call
    /// </summary>
    public required string FunctionName { get; init; }
    
    /// <summary>
    /// JSON string containing the arguments
    /// </summary>
    public required string Arguments { get; init; }
}

