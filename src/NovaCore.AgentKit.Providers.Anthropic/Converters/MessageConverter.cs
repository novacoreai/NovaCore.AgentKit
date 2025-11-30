using System.Text.Json;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Anthropic.Models;

namespace NovaCore.AgentKit.Providers.Anthropic.Converters;

/// <summary>
/// Converts between our LlmMessage format and Anthropic API format
/// </summary>
public static class MessageConverter
{
    /// <summary>
    /// Convert our LlmMessage to Anthropic format
    /// </summary>
    public static (List<AnthropicMessage> Messages, string? SystemPrompt) ConvertFromLlmMessages(
        List<LlmMessage> llmMessages)
    {
        var anthropicMessages = new List<AnthropicMessage>();
        string? systemPrompt = null;
        
        foreach (var msg in llmMessages)
        {
            // Extract system message separately
            if (msg.Role == MessageRole.System)
            {
                systemPrompt = msg.Text;
                continue;
            }
            
            // Map role
            var role = msg.Role switch
            {
                MessageRole.Assistant => "assistant",
                MessageRole.User => "user",
                MessageRole.Tool => "user", // Tool results come back as user messages in Anthropic
                _ => "user"
            };
            
            // Build content blocks
            var contentBlocks = new List<AnthropicContentBlock>();
            
            if (msg.Contents != null)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is TextMessageContent textContent && !string.IsNullOrEmpty(textContent.Text))
                    {
                        contentBlocks.Add(new AnthropicContentBlock
                        {
                            Type = "text",
                            Text = textContent.Text
                        });
                    }
                    else if (content is ImageMessageContent imageContent)
                    {
                        // Image or file content - convert to Anthropic image format
                        contentBlocks.Add(new AnthropicContentBlock
                        {
                            Type = "image",
                            Source = new
                            {
                                type = "base64",
                                media_type = imageContent.MimeType,
                                data = Convert.ToBase64String(imageContent.Data)
                            }
                        });
                    }
                    else if (content is ToolCallMessageContent toolCallContent)
                    {
                        // Model requesting a tool call
                        Dictionary<string, object?> input;
                        
                        // Handle empty or whitespace ArgumentsJson
                        if (string.IsNullOrWhiteSpace(toolCallContent.ArgumentsJson))
                        {
                            input = new Dictionary<string, object?>();
                        }
                        else
                        {
                            input = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCallContent.ArgumentsJson) 
                                        ?? new Dictionary<string, object?>();
                        }
                        
                        contentBlocks.Add(new AnthropicContentBlock
                        {
                            Type = "tool_use",
                            Id = toolCallContent.CallId,
                            Name = toolCallContent.ToolName,
                            Input = input
                        });
                    }
                    else if (content is ToolResultMessageContent toolResultContent)
                    {
                        // Tool result
                        contentBlocks.Add(new AnthropicContentBlock
                        {
                            Type = "tool_result",
                            ToolUseId = toolResultContent.CallId,
                            Content = toolResultContent.Result,
                            IsError = toolResultContent.IsError
                        });
                    }
                }
            }
            
            // Fall back to text if no contents
            if (!contentBlocks.Any() && !string.IsNullOrEmpty(msg.Text))
            {
                contentBlocks.Add(new AnthropicContentBlock
                {
                    Type = "text",
                    Text = msg.Text
                });
            }
            
            if (contentBlocks.Any())
            {
                // Use content blocks array if multiple, or string if single text
                object content = contentBlocks.Count == 1 && contentBlocks[0].Type == "text"
                    ? contentBlocks[0].Text!
                    : contentBlocks;
                
                anthropicMessages.Add(new AnthropicMessage
                {
                    Role = role,
                    Content = content
                });
            }
        }
        
        return (anthropicMessages, systemPrompt);
    }
    
    /// <summary>
    /// Convert Anthropic response to LlmResponse
    /// </summary>
    public static LlmResponse ConvertToLlmResponse(AnthropicResponse response)
    {
        string? text = null;
        var toolCalls = new List<LlmToolCall>();
        
        // Extract content blocks
        foreach (var block in response.Content)
        {
            if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
            {
                text = (text ?? "") + block.Text;
            }
            else if (block.Type == "tool_use")
            {
                var argsJson = JsonSerializer.Serialize(block.Input ?? new Dictionary<string, object?>());
                
                toolCalls.Add(new LlmToolCall
                {
                    Id = block.Id ?? Guid.NewGuid().ToString(),
                    Name = block.Name ?? "unknown",
                    ArgumentsJson = argsJson
                });
            }
        }
        
        // Extract usage info
        LlmUsage? usage = null;
        if (response.Usage != null)
        {
            usage = new LlmUsage
            {
                InputTokens = response.Usage.InputTokens,
                OutputTokens = response.Usage.OutputTokens
            };
        }
        
        return new LlmResponse
        {
            Text = text,
            ToolCalls = toolCalls.Any() ? toolCalls : null,
            FinishReason = ConvertStopReason(response.StopReason),
            Usage = usage
        };
    }
    
    /// <summary>
    /// Convert our LlmTools to Anthropic tool format
    /// </summary>
    public static List<AnthropicTool> ConvertToAnthropicTools(Dictionary<string, LlmTool> tools)
    {
        var result = new List<AnthropicTool>();
        
        foreach (var kvp in tools)
        {
            var tool = kvp.Value;
            var schemaElement = tool.ParameterSchema;
            
            // Extract properties and required from schema
            var properties = new Dictionary<string, object>();
            var required = new List<string>();
            
            if (schemaElement.TryGetProperty("properties", out var propsElement))
            {
                var propsDict = ConvertJsonElementToDictionary(propsElement);
                properties = propsDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? (object)string.Empty);
            }
            
            if (schemaElement.TryGetProperty("required", out var requiredElement) 
                && requiredElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var req in requiredElement.EnumerateArray())
                {
                    if (req.ValueKind == JsonValueKind.String)
                    {
                        required.Add(req.GetString()!);
                    }
                }
            }
            
            result.Add(new AnthropicTool
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = new AnthropicInputSchema
                {
                    Type = "object",
                    Properties = properties.Any() ? properties : null,
                    Required = required.Any() ? required : null
                }
            });
        }
        
        return result;
    }
    
    private static LlmFinishReason? ConvertStopReason(string? stopReason)
    {
        return stopReason switch
        {
            "end_turn" => LlmFinishReason.Stop,
            "max_tokens" => LlmFinishReason.Length,
            "tool_use" => LlmFinishReason.ToolCalls,
            "stop_sequence" => LlmFinishReason.Stop,
            _ => null
        };
    }
    
    private static IDictionary<string, object?> ParseToolArguments(object? input)
    {
        if (input == null)
        {
            return new Dictionary<string, object?>();
        }
        
        // If it's already a dictionary, return it
        if (input is IDictionary<string, object?> dict)
        {
            return dict;
        }
        
        // If it's a JsonElement, convert it
        if (input is JsonElement jsonElement)
        {
            return ConvertJsonElementToDictionary(jsonElement);
        }
        
        // Try to serialize and deserialize
        try
        {
            var json = JsonSerializer.Serialize(input);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) 
                   ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }
    
    private static Dictionary<string, object?> ConvertJsonElementToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var prop in element.EnumerateObject())
        {
            result[prop.Name] = ConvertJsonElementToObject(prop.Value);
        }
        
        return result;
    }
    
    private static object? ConvertJsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }
}

