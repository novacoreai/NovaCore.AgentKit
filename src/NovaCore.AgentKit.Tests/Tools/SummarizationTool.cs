using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using NovaCore.AgentKit.Core;
using ChatMessage = NovaCore.AgentKit.Core.ChatMessage;
using ChatRole = NovaCore.AgentKit.Core.ChatRole;

namespace NovaCore.AgentKit.Tests.Tools;

/// <summary>
/// Summarization tool for testing auto-checkpointing
/// </summary>
public class TestSummarizationTool : Tool<SummaryArgs, SummaryResult>
{
    private readonly IChatClient _summarizerLlm;
    
    public TestSummarizationTool(IChatClient summarizerLlm)
    {
        _summarizerLlm = summarizerLlm;
    }
    
    public override string Name => "summarize_conversation";
    
    public override string Description => "Summarizes conversation history segments";
    
    protected override async Task<SummaryResult> ExecuteAsync(SummaryArgs args, CancellationToken ct)
    {
        // Build prompt from conversation messages
        var prompt = "Summarize this conversation segment in 1-2 concise sentences:\n\n";
        
        foreach (var msg in args.Messages)
        {
            if (!string.IsNullOrEmpty(msg.Text))
            {
                prompt += $"{msg.Role}: {msg.Text}\n";
            }
        }
        
        prompt += "\nSummary:";
        
        // Call LLM for summarization using streaming API
        var aiMessages = new List<Microsoft.Extensions.AI.ChatMessage> 
        { 
            new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.User, 
                prompt) 
        };
        
        var textParts = new List<string>();
        await foreach (var update in _summarizerLlm.GetStreamingResponseAsync(aiMessages, null, ct))
        {
            if (update.Text != null)
            {
                textParts.Add(update.Text);
            }
        }
        
        var summary = string.Concat(textParts);
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "Summary unavailable";
        }
        
        return new SummaryResult(Summary: summary);
    }
}

public record SummaryArgs(
    [property: JsonPropertyName("conversation_id")] string ConversationId,
    [property: JsonPropertyName("from_turn")] int FromTurn,
    [property: JsonPropertyName("to_turn")] int ToTurn,
    [property: JsonPropertyName("original_message_count")] int OriginalMessageCount,
    [property: JsonPropertyName("filtered_message_count")] int FilteredMessageCount,
    [property: JsonPropertyName("messages")] List<MessageInfo> Messages
);

public record MessageInfo(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("has_tool_calls")] bool HasToolCalls,
    [property: JsonPropertyName("is_tool_result")] bool IsToolResult
);

public record SummaryResult(
    [property: JsonPropertyName("summary")] string Summary
);

