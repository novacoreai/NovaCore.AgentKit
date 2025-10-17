namespace NovaCore.AgentKit.Core;

/// <summary>
/// Verbosity level for logging
/// </summary>
public enum LogVerbosity
{
    /// <summary>Don't log this item</summary>
    None,
    
    /// <summary>Log with truncation</summary>
    Truncated,
    
    /// <summary>Log full content</summary>
    Full
}

/// <summary>
/// Configuration for agent turn logging
/// </summary>
public class AgentLoggingConfig
{
    /// <summary>
    /// Whether and how to log user input
    /// </summary>
    public LogVerbosity LogUserInput { get; set; } = LogVerbosity.None;
    
    /// <summary>
    /// Whether and how to log agent output/response
    /// </summary>
    public LogVerbosity LogAgentOutput { get; set; } = LogVerbosity.None;
    
    /// <summary>
    /// Whether and how to log tool call requests (tool name and arguments)
    /// </summary>
    public LogVerbosity LogToolCallRequests { get; set; } = LogVerbosity.None;
    
    /// <summary>
    /// Whether and how to log tool call responses/results
    /// </summary>
    public LogVerbosity LogToolCallResponses { get; set; } = LogVerbosity.None;
    
    /// <summary>
    /// Default truncation length when using Truncated verbosity (default: 200 characters)
    /// </summary>
    public int TruncationLength { get; set; } = 200;
    
    /// <summary>
    /// Use structured logging with properties (default: true).
    /// When false, uses simple string formatting.
    /// </summary>
    public bool UseStructuredLogging { get; set; } = true;
}

