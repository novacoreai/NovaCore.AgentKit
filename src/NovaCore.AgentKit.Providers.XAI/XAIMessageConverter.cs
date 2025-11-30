using System.Text.Json;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.XAI;

/// <summary>
/// Message converter for XAI API
/// </summary>
internal static class XAIMessageConverter
{
    public static List<XAIMessage> ConvertToXAIMessages(List<LlmMessage> messages)
    {
        var result = new List<XAIMessage>();
        
        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                MessageRole.System => "system",
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.Tool => "tool",
                _ => "user"
            };
            
            // Handle different message types
            if (msg.Role == MessageRole.Tool && msg.ToolCallId != null)
            {
                // Tool result message
                result.Add(new XAIMessage
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
                List<XAIToolCall>? toolCalls = null;
                
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
                        toolCalls ??= new List<XAIToolCall>();
                        toolCalls.Add(new XAIToolCall
                        {
                            Id = toolCall.CallId,
                            Type = "function",
                            Function = new XAIFunctionCall
                            {
                                Name = toolCall.ToolName,
                                Arguments = toolCall.ArgumentsJson
                            }
                        });
                    }
                }
                
                object? messageContent = contentParts.Count > 0 ? contentParts : null;
                
                result.Add(new XAIMessage
                {
                    Role = role,
                    Content = messageContent,
                    ToolCalls = toolCalls
                });
            }
            else
            {
                // Simple text message
                result.Add(new XAIMessage
                {
                    Role = role,
                    Content = msg.Text
                });
            }
        }
        
        return result;
    }
    
    public static List<XAITool> ConvertToXAITools(Dictionary<string, LlmTool> tools)
    {
        var result = new List<XAITool>();
        
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
            
            result.Add(new XAITool
            {
                Type = "function",
                Function = new XAIFunction
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

