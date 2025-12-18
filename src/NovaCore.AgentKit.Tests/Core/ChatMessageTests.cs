using Microsoft.Extensions.AI;
using NovaCore.AgentKit.Core;
using Xunit;
using ChatMessage = NovaCore.AgentKit.Core.ChatMessage;
using ChatRole = NovaCore.AgentKit.Core.ChatRole;

namespace NovaCore.AgentKit.Tests.Core;

/// <summary>
/// Tests for ChatMessage construction and properties
/// </summary>
public class ChatMessageTests
{
    [Fact]
    public void ChatMessage_TextConstructor_Works()
    {
        var message = new ChatMessage(ChatRole.User, "Hello");
        
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Equal("Hello", message.Text);
        Assert.Null(message.ToolCalls);
        Assert.Null(message.ToolCallId);
    }
    
    [Fact]
    public void ChatMessage_MultimodalConstructor_Works()
    {
        var contents = new List<IMessageContent>
        {
            new TextMessageContent("Hello"),
            new ImageMessageContent(new byte[] { 1, 2, 3 }, "image/png")
        };
        
        var message = new ChatMessage(ChatRole.User, contents);
        
        Assert.Equal(ChatRole.User, message.Role);
        Assert.NotNull(message.Contents);
        Assert.Equal(2, message.Contents.Count);
        Assert.Equal("Hello", message.Text); // Should extract from first TextContent
    }
    
    [Fact]
    public void ChatMessage_ToolResult_Works()
    {
        var message = new ChatMessage(ChatRole.Tool, "{\"result\": 42}", "call_123");
        
        Assert.Equal(ChatRole.Tool, message.Role);
        Assert.Equal("{\"result\": 42}", message.Text);
        Assert.Equal("call_123", message.ToolCallId);
    }
    
    [Fact]
    public void ChatMessage_WithToolCalls_Works()
    {
        var toolCalls = new List<ToolCall>
        {
            new ToolCall
            {
                Id = "call_1",
                FunctionName = "calculator",
                Arguments = "{\"a\": 5, \"b\": 3}"
            }
        };
        
        var message = new ChatMessage(ChatRole.Assistant, "I'll calculate that", toolCalls);
        
        Assert.Equal(ChatRole.Assistant, message.Role);
        Assert.Equal("I'll calculate that", message.Text);
        Assert.NotNull(message.ToolCalls);
        Assert.Single(message.ToolCalls);
        Assert.Equal("calculator", message.ToolCalls[0].FunctionName);
    }
}

