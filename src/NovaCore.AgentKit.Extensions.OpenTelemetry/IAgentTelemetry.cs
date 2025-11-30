namespace NovaCore.AgentKit.Extensions.OpenTelemetry;

/// <summary>
/// Telemetry interface for agent operations
/// </summary>
public interface IAgentTelemetry
{
    /// <summary>
    /// Record a completed agent turn
    /// </summary>
    void RecordTurn(string agentType, TimeSpan duration, int toolCalls);
    
    /// <summary>
    /// Record an error
    /// </summary>
    void RecordError(string agentType, Exception exception);
    
    /// <summary>
    /// Record a tool execution
    /// </summary>
    void RecordToolExecution(string toolName, TimeSpan duration, bool success);
}

