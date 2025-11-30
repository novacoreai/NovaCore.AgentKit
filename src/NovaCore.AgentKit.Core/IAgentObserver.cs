namespace NovaCore.AgentKit.Core;

/// <summary>
/// Observer interface for agent execution events.
/// Implement only the methods you need - all have default no-op implementations.
/// </summary>
public interface IAgentObserver
{
    /// <summary>
    /// Called when a turn starts (user message received)
    /// </summary>
    void OnTurnStart(TurnStartEvent evt) { }
    
    /// <summary>
    /// Called when a turn completes successfully
    /// </summary>
    void OnTurnComplete(TurnCompleteEvent evt) { }
    
    /// <summary>
    /// Called before making an LLM API call
    /// </summary>
    void OnLlmRequest(LlmRequestEvent evt) { }
    
    /// <summary>
    /// Called after LLM API call completes
    /// </summary>
    void OnLlmResponse(LlmResponseEvent evt) { }
    
    /// <summary>
    /// Called when a tool starts executing
    /// </summary>
    void OnToolExecutionStart(ToolExecutionStartEvent evt) { }
    
    /// <summary>
    /// Called when a tool completes execution
    /// </summary>
    void OnToolExecutionComplete(ToolExecutionCompleteEvent evt) { }
    
    /// <summary>
    /// Called when an error occurs during execution
    /// </summary>
    void OnError(ErrorEvent evt) { }
}

/// <summary>
/// Shared context information for all agent events
/// </summary>
public record AgentEventContext
{
    /// <summary>Timestamp when the event occurred</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>Conversation ID (null for ReActAgent)</summary>
    public string? ConversationId { get; init; }
    
    /// <summary>Current number of messages in history</summary>
    public int MessageCount { get; init; }
}

/// <summary>
/// Event fired when a turn starts
/// </summary>
public record TurnStartEvent(
    AgentEventContext Context, 
    string UserMessage);

/// <summary>
/// Event fired when a turn completes
/// </summary>
public record TurnCompleteEvent(
    AgentEventContext Context, 
    AgentTurn Result, 
    TimeSpan Duration);

/// <summary>
/// Event fired before making an LLM API call
/// </summary>
public record LlmRequestEvent(
    AgentEventContext Context,
    IReadOnlyList<LlmMessage> Messages,
    int ToolCount);

/// <summary>
/// Event fired after LLM API call completes
/// </summary>
public record LlmResponseEvent(
    AgentEventContext Context,
    string ModelName,
    string? Text,
    List<LlmToolCall>? ToolCalls,
    LlmUsage? Usage,
    LlmFinishReason? FinishReason,
    TimeSpan Duration);

/// <summary>
/// Event fired when a tool starts executing
/// </summary>
public record ToolExecutionStartEvent(
    AgentEventContext Context,
    string ToolName,
    string Arguments);

/// <summary>
/// Event fired when a tool completes execution
/// </summary>
public record ToolExecutionCompleteEvent(
    AgentEventContext Context,
    string ToolName,
    string Result,
    TimeSpan Duration,
    Exception? Error = null);

/// <summary>
/// Event fired when an error occurs
/// </summary>
public record ErrorEvent(
    AgentEventContext Context,
    Exception Exception,
    string Phase);

