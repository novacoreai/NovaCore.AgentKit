namespace NovaCore.AgentKit.Core.TurnValidation;

/// <summary>
/// Validates conversation turn sequences follow LLM API rules
/// </summary>
public interface ITurnValidator
{
    /// <summary>
    /// Validate conversation turns follow LLM API rules
    /// </summary>
    TurnValidationResult Validate(List<ChatMessage> history);
    
    /// <summary>
    /// Fix invalid turn sequences (if possible)
    /// </summary>
    List<ChatMessage> Fix(List<ChatMessage> history);
}

