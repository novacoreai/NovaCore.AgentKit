namespace NovaCore.AgentKit.Core;

/// <summary>
/// Simple composite observer that forwards agent events to multiple observers.
/// Used to attach built-in instrumentation without preventing host observers.
/// </summary>
public sealed class CompositeAgentObserver : IAgentObserver
{
    private readonly IReadOnlyList<IAgentObserver> _observers;
    
    public CompositeAgentObserver(IEnumerable<IAgentObserver> observers)
    {
        _observers = observers.Where(o => o != null).ToList();
    }
    
    public IReadOnlyList<IAgentObserver> Observers => _observers;
    
    public void OnTurnStart(TurnStartEvent evt)
    {
        foreach (var o in _observers) Safe(() => o.OnTurnStart(evt));
    }
    
    public void OnTurnComplete(TurnCompleteEvent evt)
    {
        foreach (var o in _observers) Safe(() => o.OnTurnComplete(evt));
    }
    
    public void OnLlmRequest(LlmRequestEvent evt)
    {
        foreach (var o in _observers) Safe(() => o.OnLlmRequest(evt));
    }
    
    public void OnLlmResponse(LlmResponseEvent evt)
    {
        foreach (var o in _observers) Safe(() => o.OnLlmResponse(evt));
    }
    
    public void OnToolExecutionStart(ToolExecutionStartEvent evt)
    {
        foreach (var o in _observers) Safe(() => o.OnToolExecutionStart(evt));
    }
    
    public void OnToolExecutionComplete(ToolExecutionCompleteEvent evt)
    {
        foreach (var o in _observers) Safe(() => o.OnToolExecutionComplete(evt));
    }
    
    public void OnError(ErrorEvent evt)
    {
        foreach (var o in _observers) Safe(() => o.OnError(evt));
    }
    
    private static void Safe(Action action)
    {
        try { action(); }
        catch { /* never let observers break agent execution */ }
    }
}


