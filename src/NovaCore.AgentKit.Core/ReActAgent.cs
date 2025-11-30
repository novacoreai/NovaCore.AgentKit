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
    /// Run the agent to complete a task with a ChatMessage (supports multimodal content)
    /// </summary>
    public async Task<ReActResult> RunAsync(ChatMessage taskMessage, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var totalLlmCalls = 0;
        var turnCount = 0;
        string lastResponse = "";
        
        // Add task message to history with ReAct instructions
        var taskText = taskMessage.Text ?? "";
        var initialPrompt = BuildReActPrompt(taskText);
        
        // Get initial files from the task message if present
        List<FileAttachment>? initialFiles = null;
        if (taskMessage.Contents?.Any() == true)
        {
            // Extract image content as file attachments for the first turn
            var imageContents = taskMessage.Contents.OfType<ImageMessageContent>().ToList();
            if (imageContents.Any())
            {
                initialFiles = imageContents.Select(img => 
                    new FileAttachment 
                    { 
                        Data = img.Data, 
                        MediaType = img.MimeType 
                    }).ToList();
            }
        }
        
        for (int i = 0; i < _config.MaxTurns; i++)
        {
            turnCount++;
            
            var message = i == 0 ? initialPrompt : "Continue with the next step.";
            var files = i == 0 ? initialFiles : null;
            var turn = await _agent.ExecuteTurnAsync(message, files, ct);
            
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
    
    /// <summary>
    /// Convenience method: Run the agent to complete a task with text only
    /// </summary>
    public async Task<ReActResult> RunAsync(string task, CancellationToken ct = default)
    {
        return await RunAsync(new ChatMessage(ChatRole.User, task), ct);
    }
    
    /// <summary>
    /// Convenience method: Run the agent to complete a task with text and file attachments
    /// </summary>
    public async Task<ReActResult> RunAsync(
        string task,
        List<FileAttachment> files,
        CancellationToken ct = default)
    {
        var contents = new List<IMessageContent> 
        { 
            new TextMessageContent(task) 
        };
        contents.AddRange(files.Select(f => f.ToMessageContent()));
        
        return await RunAsync(new ChatMessage(ChatRole.User, contents), ct);
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

