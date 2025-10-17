using System.Text.Json;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Represents a tool that can be invoked by an AI agent
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique name of the tool
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// JSON Schema describing the tool's parameters
    /// </summary>
    JsonDocument ParameterSchema { get; }
    
    /// <summary>
    /// Invoke the tool with JSON arguments
    /// </summary>
    /// <param name="argsJson">JSON string containing tool arguments</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tool execution result as string</returns>
    Task<string> InvokeAsync(string argsJson, CancellationToken ct = default);
}

