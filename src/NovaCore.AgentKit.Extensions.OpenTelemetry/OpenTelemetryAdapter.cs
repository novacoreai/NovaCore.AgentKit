using System.Diagnostics.Metrics;

namespace NovaCore.AgentKit.Extensions.OpenTelemetry;

/// <summary>
/// OpenTelemetry adapter for agent metrics
/// </summary>
public class OpenTelemetryAdapter : IAgentTelemetry
{
    private readonly Meter _meter;
    private readonly Counter<long> _turnCounter;
    private readonly Histogram<double> _turnDuration;
    private readonly Counter<long> _toolCounter;
    private readonly Histogram<double> _toolDuration;
    private readonly Counter<long> _errorCounter;
    
    public OpenTelemetryAdapter(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("NovaCore.AgentKit");
        
        _turnCounter = _meter.CreateCounter<long>(
            "agent_turns_total",
            description: "Total number of agent turns");
        
        _turnDuration = _meter.CreateHistogram<double>(
            "agent_turn_duration_seconds",
            description: "Duration of agent turns");
        
        _toolCounter = _meter.CreateCounter<long>(
            "tool_executions_total",
            description: "Total number of tool executions");
        
        _toolDuration = _meter.CreateHistogram<double>(
            "tool_execution_duration_seconds",
            description: "Duration of tool executions");
        
        _errorCounter = _meter.CreateCounter<long>(
            "agent_errors_total",
            description: "Total number of agent errors");
    }
    
    public void RecordTurn(string agentType, TimeSpan duration, int toolCalls)
    {
        _turnCounter.Add(1, new KeyValuePair<string, object?>("agent_type", agentType));
        _turnDuration.Record(duration.TotalSeconds, 
            new KeyValuePair<string, object?>("agent_type", agentType));
    }
    
    public void RecordError(string agentType, Exception exception)
    {
        _errorCounter.Add(1,
            new KeyValuePair<string, object?>("agent_type", agentType),
            new KeyValuePair<string, object?>("error_type", exception.GetType().Name));
    }
    
    public void RecordToolExecution(string toolName, TimeSpan duration, bool success)
    {
        _toolCounter.Add(1,
            new KeyValuePair<string, object?>("tool_name", toolName),
            new KeyValuePair<string, object?>("success", success));
        
        _toolDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("tool_name", toolName));
    }
}

