using System.Text.Json;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.Groq;

/// <summary>
/// Message converter for Groq API
/// </summary>
internal static class GroqMessageConverter
{
    public static List<GroqMessage> ConvertToGroqMessages(List<LlmMessage> messages)
    {
        var result = new List<GroqMessage>();
        
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
                result.Add(new GroqMessage
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
                List<GroqToolCall>? toolCalls = null;
                
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
                        toolCalls ??= new List<GroqToolCall>();
                        toolCalls.Add(new GroqToolCall
                        {
                            Id = toolCall.CallId,
                            Type = "function",
                            Function = new GroqFunctionCall
                            {
                                Name = toolCall.ToolName,
                                Arguments = toolCall.ArgumentsJson
                            }
                        });
                    }
                }
                
                object? messageContent = contentParts.Count > 0 ? contentParts : null;
                
                result.Add(new GroqMessage
                {
                    Role = role,
                    Content = messageContent,
                    ToolCalls = toolCalls
                });
            }
            else
            {
                // Simple text message
                result.Add(new GroqMessage
                {
                    Role = role,
                    Content = msg.Text
                });
            }
        }
        
        return result;
    }
    
    public static List<GroqTool> ConvertToGroqTools(Dictionary<string, LlmTool> tools)
    {
        var result = new List<GroqTool>();
        
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
            
            result.Add(new GroqTool
            {
                Type = "function",
                Function = new GroqFunction
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

