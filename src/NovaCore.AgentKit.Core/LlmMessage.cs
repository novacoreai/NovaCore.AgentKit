namespace NovaCore.AgentKit.Core;

/// <summary>
/// Message sent to/from LLM (internal format for provider communication)
/// </summary>
public class LlmMessage
{
    /// <summary>Role of the message</summary>
    public required MessageRole Role { get; init; }
    
    /// <summary>Text content (for simple messages)</summary>
    public string? Text { get; init; }
    
    /// <summary>Multimodal content items</summary>
    public List<IMessageContent>? Contents { get; init; }
    
    /// <summary>Tool call ID (for tool result messages)</summary>
    public string? ToolCallId { get; init; }
}

/// <summary>
/// Message role for LLM communication
/// </summary>
public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary>
/// Base interface for message content
/// </summary>
public interface IMessageContent
{
    string ContentType { get; }
}

/// <summary>
/// Text content
/// </summary>
public record TextMessageContent(string Text) : IMessageContent
{
    public string ContentType => "text";
}

/// <summary>
/// Image/file content
/// </summary>
public record ImageMessageContent(byte[] Data, string MimeType) : IMessageContent
{
    public string ContentType => "image";
}

/// <summary>
/// Tool call request (from assistant)
/// </summary>
public record ToolCallMessageContent(string CallId, string ToolName, string ArgumentsJson) : IMessageContent
{
    public string ContentType => "tool_call";
}

/// <summary>
/// Tool result (from tool execution)
/// </summary>
public record ToolResultMessageContent(string CallId, string Result, bool IsError = false) : IMessageContent
{
    public string ContentType => "tool_result";
}

