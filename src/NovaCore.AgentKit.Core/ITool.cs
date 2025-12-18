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

/// <summary>
/// Extended tool interface that supports returning multimodal content (images, audio, etc.)
/// alongside the text result. The additional content will be included in the user message
/// that accompanies the tool result.
/// </summary>
public interface IMultimodalTool : ITool
{
    /// <summary>
    /// Invoke the tool with JSON arguments and return a multimodal result.
    /// </summary>
    /// <param name="argsJson">JSON string containing tool arguments</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tool execution result with optional additional content</returns>
    Task<ToolResult> InvokeWithResultAsync(string argsJson, CancellationToken ct = default);
}

/// <summary>
/// Result from a multimodal tool invocation.
/// Contains the text result (for the tool call response) and optional additional content
/// (e.g., images, audio) to be included in the accompanying user message.
/// </summary>
public class ToolResult
{
    /// <summary>
    /// Text result of the tool execution. This is sent as the tool call result.
    /// </summary>
    public required string Text { get; init; }
    
    /// <summary>
    /// Optional additional content (image, audio, etc.) to include in the user message
    /// that accompanies the tool result. Used for multimodal scenarios like computer use.
    /// </summary>
    public IMessageContent? AdditionalContent { get; init; }
    
    /// <summary>
    /// Implicit conversion from string for backwards compatibility.
    /// </summary>
    public static implicit operator ToolResult(string text) => new ToolResult { Text = text };
}

