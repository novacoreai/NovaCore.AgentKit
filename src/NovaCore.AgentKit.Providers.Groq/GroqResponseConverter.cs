using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.Groq;

/// <summary>
/// Response converter for Groq API
/// </summary>
internal static class GroqResponseConverter
{
    public static LlmResponse ConvertToLlmResponse(GroqResponse response)
    {
        if (response.Choices == null || !response.Choices.Any())
        {
            return new LlmResponse
            {
                Text = "",
                ToolCalls = null,
                FinishReason = null,
                Usage = null
            };
        }
        
        var choice = response.Choices[0];
        var message = choice.Message;
        
        if (message == null)
        {
            return new LlmResponse { Text = "" };
        }
        
        // Extract text
        string? text = message.Content?.ToString();
        
        // Extract tool calls
        List<LlmToolCall>? toolCalls = null;
        if (message.ToolCalls != null && message.ToolCalls.Any())
        {
            toolCalls = message.ToolCalls.Select(tc => new LlmToolCall
            {
                Id = tc.Id,
                Name = tc.Function.Name,
                ArgumentsJson = tc.Function.Arguments
            }).ToList();
        }
        
        // Extract usage
        LlmUsage? usage = null;
        if (response.Usage != null)
        {
            usage = new LlmUsage
            {
                InputTokens = response.Usage.PromptTokens,
                OutputTokens = response.Usage.CompletionTokens
            };
        }
        
        // Map finish reason
        LlmFinishReason? finishReason = choice.FinishReason switch
        {
            "stop" => LlmFinishReason.Stop,
            "length" => LlmFinishReason.Length,
            "tool_calls" => LlmFinishReason.ToolCalls,
            "content_filter" => LlmFinishReason.ContentFilter,
            _ => null
        };
        
        return new LlmResponse
        {
            Text = text,
            ToolCalls = toolCalls,
            FinishReason = finishReason,
            Usage = usage
        };
    }
    
    public static async IAsyncEnumerable<LlmStreamingUpdate> StreamResponseAsync(
        GroqRestClient restClient,
        GroqRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Track streaming state for tool calls (they come in pieces)
        var toolCallBuilders = new Dictionary<int, ToolCallBuilder>();
        GroqUsage? usage = null;
        string? finishReason = null;
        
        await foreach (var chunk in restClient.CreateChatCompletionStreamAsync(request, cancellationToken))
        {
            GroqStreamChunk? streamChunk = null;
            
            try
            {
                streamChunk = JsonSerializer.Deserialize<GroqStreamChunk>(chunk);
            }
            catch (JsonException)
            {
                continue;
            }
            
            if (streamChunk == null)
                continue;
            
            // Collect usage if present (typically in final chunk)
            if (streamChunk.Usage != null)
            {
                usage = streamChunk.Usage;
            }
            
            if (streamChunk.Choices == null || !streamChunk.Choices.Any())
                continue;
            
            var choice = streamChunk.Choices[0];
            var delta = choice.Delta;
            
            // Capture finish reason
            if (choice.FinishReason != null)
            {
                finishReason = choice.FinishReason;
            }
            
            if (delta == null)
                continue;
            
            // Handle text delta
            if (!string.IsNullOrEmpty(delta.Content))
            {
                yield return new LlmStreamingUpdate
                {
                    TextDelta = delta.Content
                };
            }
            
            // Handle tool call deltas
            if (delta.ToolCalls != null)
            {
                foreach (var toolCallDelta in delta.ToolCalls)
                {
                    if (!toolCallBuilders.ContainsKey(toolCallDelta.Index))
                    {
                        toolCallBuilders[toolCallDelta.Index] = new ToolCallBuilder();
                    }
                    
                    var builder = toolCallBuilders[toolCallDelta.Index];
                    
                    if (toolCallDelta.Id != null)
                    {
                        builder.Id = toolCallDelta.Id;
                    }
                    
                    if (toolCallDelta.Function?.Name != null)
                    {
                        builder.Name = toolCallDelta.Function.Name;
                    }
                    
                    if (toolCallDelta.Function?.Arguments != null)
                    {
                        builder.ArgumentsJson += toolCallDelta.Function.Arguments;
                    }
                }
            }
            
            // When finished, yield complete tool calls
            if (choice.FinishReason == "tool_calls")
            {
                foreach (var builder in toolCallBuilders.Values)
                {
                    if (builder.Id != null && builder.Name != null)
                    {
                        yield return new LlmStreamingUpdate
                        {
                            ToolCall = new LlmToolCall
                            {
                                Id = builder.Id,
                                Name = builder.Name,
                                ArgumentsJson = builder.ArgumentsJson ?? "{}"
                            }
                        };
                    }
                }
            }
        }
        
        // Yield final update with usage and finish reason
        if (usage != null || finishReason != null)
        {
            yield return new LlmStreamingUpdate
            {
                Usage = usage != null ? new LlmUsage
                {
                    InputTokens = usage.PromptTokens,
                    OutputTokens = usage.CompletionTokens
                } : null,
                FinishReason = finishReason switch
                {
                    "stop" => LlmFinishReason.Stop,
                    "length" => LlmFinishReason.Length,
                    "tool_calls" => LlmFinishReason.ToolCalls,
                    "content_filter" => LlmFinishReason.ContentFilter,
                    _ => null
                }
            };
        }
    }
    
    private class ToolCallBuilder
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? ArgumentsJson { get; set; }
    }
}

