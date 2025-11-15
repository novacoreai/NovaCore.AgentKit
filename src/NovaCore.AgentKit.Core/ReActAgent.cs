namespace NovaCore.AgentKit.Core;

/// <summary>
/// Autonomous ReAct (Reasoning + Acting) agent for task execution
/// </summary>
public class ReActAgent : IAsyncDisposable
{
    private readonly Agent _agent;
    private readonly ReActConfig _config;
    private readonly IAgentObserver? _observer;
    private readonly List<IMcpClient> _mcpClients;
    private bool _disposed;
    
    internal ReActAgent(
        Agent agent, 
        ReActConfig config, 
        IAgentObserver? observer = null,
        List<IMcpClient>? mcpClients = null)
    {
        _agent = agent;
        _config = config;
        _observer = observer;
        _mcpClients = mcpClients ?? new List<IMcpClient>();
    }
    
    /// <summary>
    /// Run the agent to complete a task
    /// </summary>
    public async Task<ReActResult> RunAsync(string task, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var totalLlmCalls = 0;
        var turnCount = 0;
        string lastResponse = "";
        
        // Initial task message with ReAct instructions
        var initialPrompt = BuildReActPrompt(task);
        
        for (int i = 0; i < _config.MaxTurns; i++)
        {
            turnCount++;
            
            var message = i == 0 ? initialPrompt : "Continue with the next step.";
            var turn = await _agent.ExecuteTurnAsync(message, ct);
            
            lastResponse = turn.Response;
            totalLlmCalls += turn.LlmCallsExecuted;
            
            // Check for completion
            if (turn.CompletionSignal != null)
            {
                return new ReActResult
                {
                    Success = true,
                    FinalAnswer = turn.CompletionSignal,
                    TurnsExecuted = turnCount,
                    TotalLlmCalls = totalLlmCalls,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            
            // Check if agent seems stuck (no LLM calls with tool execution)
            if (_config.DetectStuckAgent && turn.LlmCallsExecuted == 0 && i > 2)
            {
                if (_config.BreakOnStuck)
                {
                    return new ReActResult
                    {
                        Success = false,
                        FinalAnswer = turn.Response,
                        TurnsExecuted = turnCount,
                        TotalLlmCalls = totalLlmCalls,
                        Duration = DateTime.UtcNow - startTime,
                        Error = "Agent stuck without making progress"
                    };
                }
            }
        }
        
        return new ReActResult
        {
            Success = false,
            FinalAnswer = lastResponse,
            TurnsExecuted = turnCount,
            TotalLlmCalls = totalLlmCalls,
            Duration = DateTime.UtcNow - startTime,
            Error = $"Maximum turns ({_config.MaxTurns}) reached"
        };
    }
    
    private string BuildReActPrompt(string task)
    {
        return $@"{task}

    Explain your reasoning and progress and next steps briefly. (text output)
    Then use available tools to complete this task step by step. (tool calls)
    When finished, call the 'complete_task' tool. (tool call)";

    }
    
    /// <summary>
    /// Dispose of MCP clients and resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        // Dispose all MCP clients
        foreach (var mcpClient in _mcpClients)
        {
            try
            {
                await mcpClient.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration for ReAct agent
/// </summary>
public class ReActConfig
{
    /// <summary>Maximum turns before giving up</summary>
    public int MaxTurns { get; set; } = 20;
    
    /// <summary>Detect when agent is stuck (no LLM calls)</summary>
    public bool DetectStuckAgent { get; set; } = true;
    
    /// <summary>Break execution when stuck is detected</summary>
    public bool BreakOnStuck { get; set; } = false;
}

/// <summary>
/// Result of ReAct agent execution
/// </summary>
public class ReActResult
{
    /// <summary>Whether the task was completed successfully</summary>
    public required bool Success { get; init; }
    
    /// <summary>Final answer or result</summary>
    public required string FinalAnswer { get; init; }
    
    /// <summary>Number of turns executed</summary>
    public int TurnsExecuted { get; init; }
    
    /// <summary>Total number of LLM calls across all turns</summary>
    public int TotalLlmCalls { get; init; }
    
    /// <summary>Total execution duration</summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>Error message if failed</summary>
    public string? Error { get; init; }
}

