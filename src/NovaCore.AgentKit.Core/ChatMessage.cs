namespace NovaCore.AgentKit.Core;

/// <summary>
/// Represents a message in a conversation (host application facing)
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Role of the message sender
    /// </summary>
    public ChatRole Role { get; init; }
    
    /// <summary>
    /// Text content of the message (for simple text-only messages)
    /// </summary>
    public string? Text { get; init; }
    
    /// <summary>
    /// Rich content items (text, images, files). 
    /// When set, this takes precedence over Text property for multimodal messages.
    /// </summary>
    public List<IMessageContent>? Contents { get; init; }
    
    /// <summary>
    /// Tool calls made by the assistant (if any)
    /// </summary>
    public List<ToolCall>? ToolCalls { get; init; }
    
    /// <summary>
    /// ID of the tool call this message is responding to (for Tool role messages)
    /// </summary>
    public string? ToolCallId { get; init; }
    
    /// <summary>
    /// Create a new chat message with text
    /// </summary>
    public ChatMessage(ChatRole role, string? text, string? toolCallId = null)
    {
        Role = role;
        Text = text;
        ToolCallId = toolCallId;
    }
    
    /// <summary>
    /// Create a new assistant message with tool calls
    /// </summary>
    public ChatMessage(ChatRole role, string? text, List<ToolCall>? toolCalls)
    {
        Role = role;
        Text = text;
        ToolCalls = toolCalls;
    }
    
    /// <summary>
    /// Create a new multimodal message with rich content (text, images, files)
    /// </summary>
    public ChatMessage(ChatRole role, List<IMessageContent> contents, string? toolCallId = null)
    {
        Role = role;
        Contents = contents;
        ToolCallId = toolCallId;
        
        // Set Text property to first text content for backward compatibility
        Text = contents.OfType<TextMessageContent>().FirstOrDefault()?.Text;
    }
}


