namespace NovaCore.AgentKit.Core.TurnValidation;

/// <summary>
/// Validates and fixes conversation turn sequences
/// </summary>
public class TurnValidator : ITurnValidator
{
    public TurnValidationResult Validate(List<ChatMessage> history)
    {
        var errors = new List<string>();
        
        if (history.Count == 0)
        {
            return new TurnValidationResult { IsValid = true, Errors = errors };
        }
        
        // Rule 1: Must start with System or User
        if (history[0].Role != ChatRole.System && history[0].Role != ChatRole.User)
        {
            errors.Add("First message must be System or User");
        }
        
        // Rule 2: User and Assistant must alternate (with special handling for tool calls)
        // Within a single turn, multiple Assistant messages are OK if separated by Tool results
        for (int i = 1; i < history.Count; i++)
        {
            var prev = history[i - 1];
            var curr = history[i];
            
            // Skip Tool messages for this check
            if (curr.Role == ChatRole.Tool || prev.Role == ChatRole.Tool)
            {
                continue;
            }
            
            // After User, expect Assistant
            if (prev.Role == ChatRole.User && curr.Role != ChatRole.Assistant)
            {
                errors.Add($"After User message, expected Assistant but got {curr.Role}");
            }
            
            // After Assistant, expect User OR Tool results (if Assistant made tool calls)
            if (prev.Role == ChatRole.Assistant && curr.Role == ChatRole.Assistant)
            {
                // Check if there are Tool messages between these two Assistants
                bool hasToolsBetween = false;
                for (int j = i - 1; j >= 0 && history[j].Role != ChatRole.User; j--)
                {
                    if (history[j].Role == ChatRole.Tool)
                    {
                        hasToolsBetween = true;
                        break;
                    }
                }
                
                // Consecutive Assistants are OK if:
                // 1. First Assistant made tool calls, AND
                // 2. There are Tool results between them
                if (!hasToolsBetween || prev.ToolCalls == null || !prev.ToolCalls.Any())
                {
                    errors.Add("Two consecutive Assistant messages without User message");
                }
            }
        }
        
        // Rule 3: Tool messages must follow Assistant with ToolCalls
        for (int i = 0; i < history.Count; i++)
        {
            if (history[i].Role == ChatRole.Tool)
            {
                // Find preceding Assistant message
                var assistantIdx = i - 1;
                while (assistantIdx >= 0 && history[assistantIdx].Role == ChatRole.Tool)
                {
                    assistantIdx--;
                }
                
                if (assistantIdx < 0 || history[assistantIdx].Role != ChatRole.Assistant)
                {
                    errors.Add("Tool message without preceding Assistant message");
                    continue;
                }
                
                var assistantMsg = history[assistantIdx];
                if (assistantMsg.ToolCalls == null || !assistantMsg.ToolCalls.Any())
                {
                    errors.Add("Tool message but Assistant didn't call any tools");
                    continue;
                }
                
                // Validate tool call ID matches
                var toolMsg = history[i];
                if (toolMsg.ToolCallId != null && 
                    !assistantMsg.ToolCalls.Any(tc => tc.Id == toolMsg.ToolCallId))
                {
                    errors.Add($"Tool message ID {toolMsg.ToolCallId} doesn't match any tool call");
                }
            }
        }
        
        return new TurnValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    public List<ChatMessage> Fix(List<ChatMessage> history)
    {
        if (history.Count == 0)
        {
            return history;
        }
        
        // Strategy: Remove invalid messages or insert placeholders
        var fixedHistory = new List<ChatMessage>();
        
        // Ensure starts with System or User
        if (history[0].Role != ChatRole.System && history[0].Role != ChatRole.User)
        {
            fixedHistory.Add(new ChatMessage(ChatRole.System, "You are a helpful assistant"));
        }
        
        // Copy valid messages
        ChatRole? lastNonToolRole = null;
        foreach (var msg in history)
        {
            if (msg.Role == ChatRole.Tool)
            {
                // Tool messages always allowed
                fixedHistory.Add(msg);
            }
            else
            {
                // Enforce alternation
                if (lastNonToolRole == ChatRole.User && msg.Role == ChatRole.User)
                {
                    // Two user messages in a row - insert assistant placeholder
                    fixedHistory.Add(new ChatMessage(ChatRole.Assistant, "I understand."));
                }
                else if (lastNonToolRole == ChatRole.Assistant && msg.Role == ChatRole.Assistant)
                {
                    // Two assistant messages - insert user placeholder
                    fixedHistory.Add(new ChatMessage(ChatRole.User, "Continue."));
                }
                
                fixedHistory.Add(msg);
                lastNonToolRole = msg.Role;
            }
        }
        
        return fixedHistory;
    }
}

