using System.Text.Json;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.OpenAI;

/// <summary>
/// Message converter for OpenAI API
/// </summary>
internal static class OpenAIMessageConverter
{
    public static List<OpenAIMessage> ConvertToOpenAIMessages(List<LlmMessage> messages)
    {
        var result = new List<OpenAIMessage>();
        
        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                MessageRole.System => "developer", // OpenAI now uses "developer" instead of "system"
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.Tool => "tool",
                _ => "user"
            };
            
            // Handle different message types
            if (msg.Role == MessageRole.Tool && msg.ToolCallId != null)
            {
                // Tool result message
                result.Add(new OpenAIMessage
                {
                    Role = "tool",
                    Content = msg.Text,
                    ToolCallId = msg.ToolCallId
                });
            }
            else if (msg.Contents != null && msg.Contents.Any())
            {
                // Multimodal or structured content
                var contentParts = new List<object>();
                List<OpenAIToolCall>? toolCalls = null;
                
                foreach (var content in msg.Contents)
                {
                    if (content is TextMessageContent text)
                    {
                        contentParts.Add(new { type = "text", text = text.Text });
                    }
                    else if (content is ImageMessageContent image)
                    {
                        contentParts.Add(new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:{image.MimeType};base64,{Convert.ToBase64String(image.Data)}"
                            }
                        });
                    }
                    else if (content is ToolCallMessageContent toolCall)
                    {
                        toolCalls ??= new List<OpenAIToolCall>();
                        toolCalls.Add(new OpenAIToolCall
                        {
                            Id = toolCall.CallId,
                            Type = "function",
                            Function = new OpenAIFunctionCall
                            {
                                Name = toolCall.ToolName,
                                Arguments = toolCall.ArgumentsJson
                            }
                        });
                    }
                }
                
                object? messageContent = contentParts.Count > 0 ? contentParts : null;
                
                result.Add(new OpenAIMessage
                {
                    Role = role,
                    Content = messageContent,
                    ToolCalls = toolCalls
                });
            }
            else
            {
                // Simple text message
                result.Add(new OpenAIMessage
                {
                    Role = role,
                    Content = msg.Text
                });
            }
        }
        
        return result;
    }
    
    public static List<OpenAITool> ConvertToOpenAITools(Dictionary<string, LlmTool> tools)
    {
        var result = new List<OpenAITool>();
        
        foreach (var kvp in tools)
        {
            var tool = kvp.Value;
            
            // Convert JsonElement to object for parameters
            object? parameters = null;
            try
            {
                var schemaJson = tool.ParameterSchema.GetRawText();
                parameters = JsonSerializer.Deserialize<object>(schemaJson);
            }
            catch
            {
                parameters = new { type = "object" };
            }
            
            result.Add(new OpenAITool
            {
                Type = "function",
                Function = new OpenAIFunction
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = parameters
                }
            });
        }
        
        return result;
    }
}

