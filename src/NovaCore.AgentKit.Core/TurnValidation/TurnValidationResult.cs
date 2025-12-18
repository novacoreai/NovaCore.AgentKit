namespace NovaCore.AgentKit.Core.TurnValidation;

/// <summary>
/// Result of turn validation
/// </summary>
public class TurnValidationResult
{
    /// <summary>
    /// Whether the conversation turns are valid
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// List of validation errors (if any)
    /// </summary>
    public List<string> Errors { get; init; } = new();
}

