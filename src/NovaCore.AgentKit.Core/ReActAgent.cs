using Microsoft.Extensions.Logging;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Autonomous ReAct (Reasoning + Acting) agent for task execution
/// </summary>
public class ReActAgent : IAsyncDisposable
{
    private readonly Agent _agent;
    private readonly ReActConfig _config;
    private readonly ILogger? _logger;
    private readonly List<IMcpClient> _mcpClients;
    private bool _disposed;
    
    internal ReActAgent(
        Agent agent, 
        ReActConfig config, 
        ILogger? logger = null,
        List<IMcpClient>? mcpClients = null)
    {
        _agent = agent;
        _config = config;
        _logger = logger;
        _mcpClients = mcpClients ?? new List<IMcpClient>();
    }
    
    /// <summary>
    /// Run the agent to complete a task
    /// </summary>
    public async Task<ReActResult> RunAsync(string task, CancellationToken ct = default)
    {
        _logger?.LogInformation("ReAct agent starting task: {Task}", task);
        
        var iterations = new List<ReActIteration>();
        var totalToolCalls = 0;
        var startTime = DateTime.UtcNow;
        
        // Initial task message with ReAct instructions
        var initialPrompt = BuildReActPrompt(task);
        
        for (int i = 0; i < _config.MaxIterations; i++)
        {
            _logger?.LogDebug("ReAct iteration {Iteration}/{Max}", i + 1, _config.MaxIterations);
            
            var message = i == 0 ? initialPrompt : "Continue with the next step.";
            var turn = await _agent.ExecuteTurnAsync(message, ct);
            
            iterations.Add(new ReActIteration
            {
                IterationNumber = i + 1,
                Thought = turn.Response,
                ToolCallsExecuted = turn.ToolCallsExecuted
            });
            
            totalToolCalls += turn.ToolCallsExecuted;
            
            // Check for completion
            if (turn.CompletionSignal != null)
            {
                _logger?.LogInformation("ReAct agent completed task after {Iterations} iterations", i + 1);
                
                return new ReActResult
                {
                    Success = true,
                    FinalAnswer = turn.CompletionSignal,
                    Iterations = iterations,
                    TotalToolCalls = totalToolCalls,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            
            // Check if agent seems stuck (no tool calls)
            if (_config.DetectStuckAgent && turn.ToolCallsExecuted == 0 && i > 2)
            {
                _logger?.LogWarning("Agent may be stuck (no tool calls in iteration {Iteration})", i + 1);
                
                if (_config.BreakOnStuck)
                {
                    return new ReActResult
                    {
                        Success = false,
                        FinalAnswer = turn.Response,
                        Iterations = iterations,
                        TotalToolCalls = totalToolCalls,
                        Duration = DateTime.UtcNow - startTime,
                        Error = "Agent stuck without making progress"
                    };
                }
            }
        }
        
        _logger?.LogWarning("ReAct agent reached max iterations ({Max})", _config.MaxIterations);
        
        return new ReActResult
        {
            Success = false,
            FinalAnswer = iterations.LastOrDefault()?.Thought ?? "",
            Iterations = iterations,
            TotalToolCalls = totalToolCalls,
            Duration = DateTime.UtcNow - startTime,
            Error = $"Maximum iterations ({_config.MaxIterations}) reached"
        };
    }
    
    private string BuildReActPrompt(string task)
    {
        return $@"{task}

Use available tools to complete this task step by step.

When finished, call the 'complete_task' tool with your answer.";
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
    /// <summary>Maximum iterations before giving up</summary>
    public int MaxIterations { get; set; } = 20;
    
    /// <summary>Detect when agent is stuck (no tool calls)</summary>
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
    
    /// <summary>All iterations performed</summary>
    public required List<ReActIteration> Iterations { get; init; }
    
    /// <summary>Total number of tool calls across all iterations</summary>
    public int TotalToolCalls { get; init; }
    
    /// <summary>Total execution duration</summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>Error message if failed</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Single iteration in ReAct loop
/// </summary>
public class ReActIteration
{
    /// <summary>Iteration number (1-based)</summary>
    public int IterationNumber { get; init; }
    
    /// <summary>Agent's thought/reasoning</summary>
    public required string Thought { get; init; }
    
    /// <summary>Number of tools called in this iteration</summary>
    public int ToolCallsExecuted { get; init; }
}

