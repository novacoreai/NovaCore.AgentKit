using NovaCore.AgentKit.Core.CostTracking;
using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.Core.Sanitization;
using NovaCore.AgentKit.Core.TurnValidation;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Fluent builder for creating agents
/// </summary>
public class AgentBuilder
{
    private ILlmClient? _llmClient;
    private string _modelName = "unknown";
    private ICostCalculator _costCalculator = new ModelPricingCalculator();
    private readonly List<ToolRegistration> _toolRegistrations = new();
    private readonly List<IUITool> _uiTools = new();
    private readonly List<IMcpConfiguration> _mcpConfigurations = new();
    private string? _systemPrompt;
    private string _conversationId = Guid.NewGuid().ToString();
    private IHistoryStore? _historyStore;
    private AgentConfig _config = new();
    private ReActConfig _reactConfig = new();
    private IAgentObserver? _observer;
    private IHistoryManager? _customHistoryManager;
    private IMcpClientFactory? _mcpClientFactory;
    
    /// <summary>
    /// Set the LLM client (provider)
    /// </summary>
    public AgentBuilder UseLlmClient(ILlmClient llmClient)
    {
        _llmClient = llmClient;
        return this;
    }
    
    /// <summary>
    /// Set the LLM client with model name for cost tracking
    /// </summary>
    internal AgentBuilder UseLlmClient(ILlmClient llmClient, string modelName)
    {
        _llmClient = llmClient;
        _modelName = modelName;
        return this;
    }
    
    /// <summary>
    /// Set the model name for cost tracking (when using custom LLM client)
    /// </summary>
    public AgentBuilder WithModel(string modelName)
    {
        _modelName = modelName;
        return this;
    }
    
    /// <summary>
    /// Add a tool to the agent
    /// </summary>
    /// <param name="tool">The tool to add</param>
    /// <param name="skipDefinition">
    /// When true, the tool is available for execution but its definition is NOT sent to the model.
    /// Useful for models pre-trained on specific tools (e.g., Gemini computer use).
    /// Default is false (tool definition is sent to model).
    /// </param>
    public AgentBuilder AddTool(ITool tool, bool skipDefinition = false)
    {
        _toolRegistrations.Add(new ToolRegistration(tool, skipDefinition));
        return this;
    }
    
    /// <summary>
    /// Add multiple tools to the agent
    /// </summary>
    public AgentBuilder AddTools(IEnumerable<ITool> tools)
    {
        _toolRegistrations.AddRange(tools.Select(t => new ToolRegistration(t, SkipDefinition: false)));
        return this;
    }
    
    /// <summary>
    /// Add a UI tool (returned to host for user interaction).
    /// UI tools pause execution and return control to the host application.
    /// The host should display appropriate UI and send the result back via SendAsync.
    /// </summary>
    public AgentBuilder AddUITool(IUITool uiTool)
    {
        _uiTools.Add(uiTool);
        return this;
    }
    
    /// <summary>
    /// Add multiple UI tools to the agent
    /// </summary>
    public AgentBuilder AddUITools(IEnumerable<IUITool> uiTools)
    {
        _uiTools.AddRange(uiTools);
        return this;
    }
    
    /// <summary>
    /// Set the system prompt
    /// </summary>
    public AgentBuilder WithSystemPrompt(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        _config.SystemPrompt = systemPrompt;
        return this;
    }
    
    /// <summary>
    /// Set the conversation ID
    /// </summary>
    public AgentBuilder ForConversation(string conversationId)
    {
        _conversationId = conversationId;
        return this;
    }
    
    /// <summary>
    /// Configure history storage
    /// </summary>
    public AgentBuilder WithHistoryStore(IHistoryStore historyStore)
    {
        _historyStore = historyStore;
        return this;
    }
    
    /// <summary>
    /// Configure agent behavior
    /// </summary>
    public AgentBuilder WithConfig(Action<AgentConfig> configure)
    {
        configure(_config);
        return this;
    }
    
    /// <summary>
    /// Configure history retention (what gets sent to the model).
    /// Full history is still stored - this only affects LLM context.
    /// </summary>
    /// <summary>
    /// [OBSOLETE] Use WithSummarization and/or WithToolResultFiltering instead.
    /// </summary>
    [Obsolete("Use WithSummarization() for ChatAgents or WithToolResultFiltering() for tool output management. This will be removed in a future version.")]
    public AgentBuilder WithHistoryRetention(Action<object> configure)
    {
        // No-op for backward compatibility
        return this;
    }
    
    /// <summary>
    /// Configure automatic summarization for long conversations (ChatAgent).
    /// When enabled, older messages are summarized into checkpoints to maintain context
    /// while reducing memory usage.
    /// </summary>
    /// <example>
    /// <code>
    /// .WithSummarization(cfg => 
    /// {
    ///     cfg.Enabled = true;
    ///     cfg.TriggerAt = 100;        // Summarize when we hit 100 messages
    ///     cfg.KeepRecent = 10;        // Keep last 10 messages (summarize first 90)
    ///     cfg.SummarizationTool = summaryTool;
    /// })
    /// </code>
    /// </example>
    public AgentBuilder WithSummarization(Action<SummarizationConfig> configure)
    {
        configure(_config.Summarization);
        return this;
    }
    
    /// <summary>
    /// Configure tool result filtering to reduce verbose tool outputs.
    /// Filtered tool results are replaced with "[Omitted]" placeholders to maintain
    /// conversation structure while reducing token count.
    /// </summary>
    /// <example>
    /// <code>
    /// .WithToolResultFiltering(cfg => 
    /// {
    ///     cfg.KeepRecent = 5;  // Keep last 5 tool results with full content
    /// })
    /// </code>
    /// </example>
    public AgentBuilder WithToolResultFiltering(Action<ToolResultConfig> configure)
    {
        configure(_config.ToolResults);
        return this;
    }
    
    /// <summary>
    /// Limit the number of multimodal messages (images, audio) retained in history
    /// sent to the model. Useful for computer use scenarios where only the most
    /// recent screenshot matters.
    /// </summary>
    /// <param name="maxMessages">
    /// Maximum number of messages with multimodal content to retain.
    /// Set to 1 for computer use scenarios.
    /// Default (null) = no limit.
    /// </param>
    /// <example>
    /// <code>
    /// // Keep only the most recent screenshot for computer use
    /// .WithMaxMultimodalMessages(1)
    /// </code>
    /// </example>
    public AgentBuilder WithMaxMultimodalMessages(int? maxMessages)
    {
        _config.MaxMultimodalMessages = maxMessages;
        return this;
    }
    
    /// <summary>
    /// [OBSOLETE] Use WithObserver instead.
    /// Configure logging for agent turns (what gets logged and how verbose)
    /// </summary>
    [Obsolete("Use WithObserver instead. This will be removed in a future version.")]
    public AgentBuilder WithLogging(Action<object> configure)
    {
        // No-op for backward compatibility
        return this;
    }
    
    /// <summary>
    /// [OBSOLETE] Use WithSummarization instead.
    /// Configure automatic checkpointing/summarization for long conversations.
    /// </summary>
    [Obsolete("Use WithSummarization instead. This will be removed in a future version.")]
    public AgentBuilder WithCheckpointing(Action<CheckpointConfig> configure)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        configure(_config.Checkpointing);
#pragma warning restore CS0618
        return this;
    }
    
    /// <summary>
    /// Configure ReAct agent behavior
    /// </summary>
    public AgentBuilder WithReActConfig(Action<ReActConfig> configure)
    {
        configure(_reactConfig);
        return this;
    }
    
    /// <summary>
    /// Set observer for agent execution events
    /// </summary>
    public AgentBuilder WithObserver(IAgentObserver observer)
    {
        _observer = observer;
        return this;
    }
    
    /// <summary>
    /// Set MCP client factory for creating MCP connections
    /// </summary>
    public AgentBuilder WithMcpClientFactory(IMcpClientFactory factory)
    {
        _mcpClientFactory = factory;
        return this;
    }
    
    /// <summary>
    /// Use custom history manager
    /// </summary>
    public AgentBuilder WithHistoryManager(IHistoryManager historyManager)
    {
        _customHistoryManager = historyManager;
        return this;
    }
    
    /// <summary>
    /// Add MCP (Model Context Protocol) server for automatic tool discovery
    /// </summary>
    /// <param name="mcpConfiguration">MCP server configuration</param>
    /// <param name="allowedTools">Optional list of tool names to expose. If null or empty, all tools are included.</param>
    /// <returns>The builder for fluent chaining</returns>
    public AgentBuilder WithMcp(IMcpConfiguration mcpConfiguration, List<string>? allowedTools = null)
    {
        // If allowedTools parameter is provided, override the configuration's AllowedTools
        if (allowedTools != null && allowedTools.Count > 0)
        {
            // Create a wrapper that overrides AllowedTools
            _mcpConfigurations.Add(new McpConfigurationWrapper(mcpConfiguration, allowedTools));
        }
        else
        {
            // Use configuration as-is (may have its own AllowedTools)
            _mcpConfigurations.Add(mcpConfiguration);
        }
        
        return this;
    }
    
    /// <summary>
    /// Build a ChatAgent for interactive conversations.
    /// Automatically loads existing conversation history if a history store is configured.
    /// </summary>
    public async Task<ChatAgent> BuildChatAgentAsync(CancellationToken ct = default)
    {
        var (agent, mcpClients) = await BuildInternalAgentAsync(_conversationId, ct);
        var chatAgent = new ChatAgent(
            agent, 
            _conversationId, 
            _historyStore, 
            mcpClients, 
            _observer,
            _config.Summarization);
        
        // Initialize (loads existing conversation if historyStore is configured)
        await chatAgent.InitializeAsync(ct);
        
        return chatAgent;
    }
    
    /// <summary>
    /// Build a ReActAgent for autonomous task execution.
    /// ReActAgent is ephemeral and does not persist conversation history.
    /// </summary>
    public async Task<ReActAgent> BuildReActAgentAsync(CancellationToken ct = default)
    {
        // Add complete_task tool for ReAct agents
        AddCompleteTaskTool();
        
        var (agent, mcpClients) = await BuildInternalAgentAsync(null, ct);
        
        // ReActAgent doesn't use history store - it's ephemeral
        return new ReActAgent(agent, _reactConfig, _observer, mcpClients);
    }
    
    /// <summary>
    /// Build a ChatAgent and load existing conversation history.
    /// DEPRECATED: Use BuildChatAgentAsync() instead - it now automatically loads history.
    /// </summary>
    [Obsolete("Use BuildChatAgentAsync() instead - it now automatically loads existing history when a history store is configured.")]
    public async Task<ChatAgent> BuildChatAgentWithHistoryAsync(CancellationToken ct = default)
    {
        return await BuildChatAgentAsync(ct);
    }
    
    private async Task<(Agent agent, List<IMcpClient> mcpClients)> BuildInternalAgentAsync(string? conversationId, CancellationToken ct = default)
    {
        if (_llmClient == null)
        {
            throw new InvalidOperationException("LLM client must be configured. Use a provider method like UseAnthropic()");
        }
        
        // Validate summarization configuration
        if (_config.Summarization.Enabled)
        {
            var configIssues = _config.Summarization.Validate();
            if (configIssues.Any())
            {
                // Validation issues - could potentially fire observer event, but skip for now
            }
        }
        
        // Initialize MCP clients and discover tools
        var mcpClients = new List<IMcpClient>();
        var discoveredTools = new List<ITool>();
        
        if (_mcpConfigurations.Any())
        {
            if (_mcpClientFactory == null)
            {
                throw new InvalidOperationException(
                    "MCP configurations provided but no MCP client factory set. " +
                    "Call WithMcpClientFactory() before adding MCP configurations.");
            }
            
            foreach (var mcpConfig in _mcpConfigurations)
            {
                try
                {
                    var mcpClient = await _mcpClientFactory.CreateClientAsync(mcpConfig, ct);
                    mcpClients.Add(mcpClient);
                    
                    // Discover tools from this MCP server
                    var toolDefs = await mcpClient.DiscoverToolsAsync(ct);
                    
                    // Apply tool filtering if AllowedTools is specified
                    var allowedTools = mcpConfig.AllowedTools;
                    if (allowedTools != null && allowedTools.Count > 0)
                    {
                        // Filter to only include tools in the allowed list
                        toolDefs = toolDefs.Where(t => allowedTools.Contains(t.Name)).ToList();
                    }
                    
                    // Convert tool definitions to ITool instances
                    foreach (var toolDef in toolDefs)
                    {
                        discoveredTools.Add(new McpToolProxy(mcpClient, toolDef));
                    }
                }
                catch (Exception)
                {
                    // Clean up any clients we've created so far
                    foreach (var client in mcpClients)
                    {
                        try
                        {
                            await client.DisposeAsync();
                        }
                        catch
                        {
                            // Ignore disposal errors
                        }
                    }
                    
                    throw;
                }
            }
        }
        
        // Combine manually added tools with MCP discovered tools
        // MCP discovered tools are always included in definitions (skipDefinition = false)
        var allToolRegistrations = new List<ToolRegistration>(_toolRegistrations);
        allToolRegistrations.AddRange(discoveredTools.Select(t => new ToolRegistration(t, SkipDefinition: false)));
        
        // UI tools are always included in definitions
        var uiToolRegistrations = _uiTools.Select(t => new ToolRegistration(t, SkipDefinition: false)).ToList();
        
        // Create history manager (simplified - no auto-compression)
        var historyManager = _customHistoryManager ?? new InMemoryHistoryManager();
        
        // Create history selector
        var historySelector = new SmartHistorySelector();
        
        // Create turn validator
        var turnValidator = new TurnValidator();
        
        // Create output sanitizer
        var sanitizer = new OutputSanitizer();
        
        var agent = new Agent(
            _llmClient,
            _modelName,
            _costCalculator,
            allToolRegistrations,
            uiToolRegistrations,
            historyManager,
            historySelector,
            turnValidator,
            sanitizer,
            _config,
            _observer,
            conversationId);
        
        return (agent, mcpClients);
    }
    
    private void AddCompleteTaskTool()
    {
        // Check if already added
        if (_toolRegistrations.Any(r => r.Tool.Name == "complete_task"))
        {
            return;
        }
        
        _toolRegistrations.Add(new ToolRegistration(new CompleteTaskTool(), SkipDefinition: false));
    }
}

/// <summary>
/// Built-in tool for ReAct agents to signal task completion
/// </summary>
internal class CompleteTaskTool : ITool
{
    public CompleteTaskTool()
    {
    }
    
    public string Name => "complete_task";
    
    public string Description => "Call this when you have completed the task";
    
    public System.Text.Json.JsonDocument ParameterSchema => System.Text.Json.JsonDocument.Parse(@"{
        ""type"": ""object"",
        ""properties"": {
            ""answer"": {
                ""type"": ""string"",
                ""description"": ""Your final answer""
            }
        },
        ""required"": [""answer""]
    }");
    
    public Task<string> InvokeAsync(string argsJson, CancellationToken ct = default)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(argsJson);
            
            // Unwrap common wrappers (args, arguments, parameters)
            if (json.TryGetProperty("args", out var argsWrapped) ||
                json.TryGetProperty("arguments", out argsWrapped) ||
                json.TryGetProperty("parameters", out argsWrapped))
            {
                json = argsWrapped;
            }
            
            // Try "answer" (primary) - handle both string and number
            if (json.TryGetProperty("answer", out var answer))
            {
                var result = answer.ValueKind == System.Text.Json.JsonValueKind.String 
                    ? answer.GetString() ?? string.Empty
                    : answer.ToString();
                
                return Task.FromResult(result);
            }
            
            // Fallback: Try "final_answer" (legacy) - handle both string and number
            if (json.TryGetProperty("final_answer", out var finalAnswer))
            {
                var result = finalAnswer.ValueKind == System.Text.Json.JsonValueKind.String 
                    ? finalAnswer.GetString() ?? string.Empty
                    : finalAnswer.ToString();
                
                return Task.FromResult(result);
            }
            
            // If entire payload is a string, use it
            if (json.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var result = json.GetString() ?? string.Empty;
                return Task.FromResult(result);
            }
            
            return Task.FromResult(string.Empty);
        }
        catch (Exception)
        {
            return Task.FromResult(string.Empty);
        }
    }
}

/// <summary>
/// Internal record to track tool registration with its configuration
/// </summary>
/// <param name="Tool">The tool instance</param>
/// <param name="SkipDefinition">
/// When true, the tool definition is NOT sent to the model.
/// The tool is still available for execution.
/// </param>
internal record ToolRegistration(ITool Tool, bool SkipDefinition);

/// <summary>
/// Internal wrapper to override AllowedTools property of an MCP configuration
/// </summary>
internal class McpConfigurationWrapper : IMcpConfiguration
{
    private readonly IMcpConfiguration _inner;
    private readonly List<string> _allowedTools;
    
    public McpConfigurationWrapper(IMcpConfiguration inner, List<string> allowedTools)
    {
        _inner = inner;
        _allowedTools = allowedTools;
    }
    
    public string Command => _inner.Command;
    public List<string> Arguments => _inner.Arguments;
    public Dictionary<string, string> Environment => _inner.Environment;
    public string? WorkingDirectory => _inner.WorkingDirectory;
    public List<string>? AllowedTools => _allowedTools;
}

