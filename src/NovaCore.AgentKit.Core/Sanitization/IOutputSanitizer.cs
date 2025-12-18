namespace NovaCore.AgentKit.Core.Sanitization;

/// <summary>
/// Sanitizes LLM output (removes thinking tags, unwraps JSON, etc.)
/// </summary>
public interface IOutputSanitizer
{
    /// <summary>
    /// Sanitize LLM output
    /// </summary>
    string Sanitize(string output, SanitizationOptions options);
}

