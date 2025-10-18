using System.Diagnostics;
using System.Text.Json;
using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.Core.Sanitization;
using NovaCore.AgentKit.Core.TurnValidation;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Internal core agent implementation
/// </summary>
internal class Agent
{
    private readonly ILlmClient _llmClient;
    private readonly List<ITool> _tools;
    private readonly HashSet<string> _uiToolNames;
    private readonly IHistoryManager _historyManager;
    private readonly IHistorySelector _historySelector;
    private readonly ITurnValidator _turnValidator;
    private readonly IOutputSanitizer _sanitizer;
    private readonly AgentConfig _config;
    private readonly IAgentObserver? _observer;
    private readonly string? _conversationId;
    private readonly Dictionary<string, JsonElement> _toolSchemas;
    private readonly Dictionary<string, ITool> _allToolsMap; // Map of all tools (regular + UI) by name
    
    public Agent(
        ILlmClient llmClient,
        List<ITool> tools,
        List<IUITool> uiTools,
        IHistoryManager historyManager,
        IHistorySelector historySelector,
        ITurnValidator turnValidator,
        IOutputSanitizer sanitizer,
        AgentConfig config,
        IAgentObserver? observer = null,
        string? conversationId = null)
    {
        _llmClient = llmClient;
        _tools = tools;
        _uiToolNames = new HashSet<string>(uiTools.Select(t => t.Name));
        _historyManager = historyManager;
        _historySelector = historySelector;
        _turnValidator = turnValidator;
        _sanitizer = sanitizer;
        _config = config;
        _observer = observer;
        _conversationId = conversationId;
        
        // Build tool schemas dictionary and all tools map (include both regular tools and UI tools)
        _toolSchemas = new Dictionary<string, JsonElement>();
        _allToolsMap = new Dictionary<string, ITool>();
        
        foreach (var tool in _tools)
        {
            _toolSchemas[tool.Name] = tool.ParameterSchema.RootElement.Clone();
            _allToolsMap[tool.Name] = tool;
        }
        foreach (var uiTool in uiTools)
        {
            _toolSchemas[uiTool.Name] = uiTool.ParameterSchema.RootElement.Clone();
            _allToolsMap[uiTool.Name] = uiTool;
        }
        
        // Add system prompt if configured with ONE tool call at a time instruction
        if (!string.IsNullOrEmpty(_config.SystemPrompt))
        {
            var history = _historyManager.GetHistory();
            if (history.Count == 0 || history[0].Role != ChatRole.System)
            {
                var enhancedPrompt = _config.SystemPrompt;
                
                // Add tool call instruction if there are tools available
                if (_tools.Any())
                {
                    enhancedPrompt += "\n\nIMPORTANT: When using tools, make ONE tool call at a time. " +
                                    "Wait for the tool result before making additional tool calls. " +
                                    "This ensures proper execution and better results.";
                }
                
                _historyManager.AddMessage(new ChatMessage(ChatRole.System, enhancedPrompt));
            }
        }
    }
    
    public async Task<AgentTurn> ExecuteTurnAsync(string userMessage, CancellationToken ct = default)
    {
        return await ExecuteTurnAsync(userMessage, files: null, ct);
    }
    
    public async Task<AgentTurn> ExecuteTurnAsync(
        string userMessage, 
        List<FileAttachment>? files = null, 
        CancellationToken ct = default)
    {
        var turnStartTime = Stopwatch.StartNew();
        
        try
        {
            // Fire turn start event
            _observer?.OnTurnStart(new TurnStartEvent(
                BuildEventContext(),
                userMessage));
            
            // Create message - multimodal if files are present
        // Check if the message is already in history (ChatAgent pre-adds messages for persistence)
        var history = _historyManager.GetHistory();
        var lastMessage = history.LastOrDefault();
        
        // Only add user message if it's not already there
        // (ChatAgent may have pre-added it, or it could be a Tool message for UI tool responses)
        bool messageAlreadyAdded = lastMessage != null && 
                                   (lastMessage.Role == ChatRole.User || lastMessage.Role == ChatRole.Tool) &&
                                   lastMessage.Text == userMessage;
        
        if (!messageAlreadyAdded)
        {
            ChatMessage userChatMessage;
            if (files?.Any() == true)
            {
                var contents = new List<IMessageContent>
                {
                    new TextMessageContent(userMessage)
                };
                
                // Add file attachments as image content
                foreach (var file in files)
                {
                    contents.Add(file.ToMessageContent());
                }
                
                userChatMessage = new ChatMessage(ChatRole.User, contents);
            }
            else
            {
                userChatMessage = new ChatMessage(ChatRole.User, userMessage);
            }
            
            // Add user message
            _historyManager.AddMessage(userChatMessage);
        }
            
            // Validate before calling LLM
            if (_config.EnableTurnValidation)
            {
                var validation = _turnValidator.Validate(_historyManager.GetHistory());
                if (!validation.IsValid)
                {
                    var fixedHistory = _turnValidator.Fix(_historyManager.GetHistory());
                    _historyManager.ReplaceHistory(fixedHistory);
                }
            }
            
            int llmCallCount = 0;
            ChatMessage? lastResponse = null;
            string? completionSignal = null;
            
            // Tool call loop
            while (true)
            {
                // Get full history from manager
                var fullHistory = _historyManager.GetHistory();
                
                // Apply tool result filtering to select context for model
                var contextHistory = _historySelector.SelectMessagesForContext(
                    fullHistory, 
                    _config.ToolResults);
                
                // Convert history to LLM format
                var llmMessages = ConvertToLlmMessages(contextHistory);
                
                // Prepare options with tools (includes both regular tools and UI tools)
                var llmOptions = new LlmOptions
                {
                    Tools = _toolSchemas.ToDictionary(
                        kvp => kvp.Key,
                        kvp =>
                        {
                            // Get tool from all tools map (includes both regular and UI tools)
                            var tool = _allToolsMap.GetValueOrDefault(kvp.Key);
                            return new LlmTool
                            {
                                Name = kvp.Key,
                                Description = tool?.Description ?? $"Execute {kvp.Key} tool",
                                ParameterSchema = kvp.Value
                            };
                        })
                };
                
                // Fire LLM request event
                _observer?.OnLlmRequest(new LlmRequestEvent(
                    BuildEventContext(),
                    llmMessages,
                    llmOptions.Tools.Count));
                
                // Call LLM - collect streaming updates
                var llmCallStartTime = Stopwatch.StartNew();
                var textParts = new List<string>();
                var collectedToolCalls = new List<LlmToolCall>();
                LlmUsage? usage = null;
                LlmFinishReason? finishReason = null;
                
                await foreach (var update in _llmClient.GetStreamingResponseAsync(llmMessages, llmOptions, ct))
                {
                    if (update.TextDelta != null)
                    {
                        textParts.Add(update.TextDelta);
                    }
                    
                    if (update.ToolCall != null)
                    {
                        collectedToolCalls.Add(update.ToolCall);
                    }
                    
                    // Capture usage and finish reason from updates (typically in the final update)
                    if (update.Usage != null)
                    {
                        usage = update.Usage;
                    }
                    
                    if (update.FinishReason != null)
                    {
                        finishReason = update.FinishReason;
                    }
                }
                
                llmCallStartTime.Stop();
                
                // Combine into final response
                var combinedText = string.Concat(textParts);
                
                // Sanitize output if enabled
                string? sanitizedText = combinedText;
                if (_config.EnableOutputSanitization && !string.IsNullOrEmpty(sanitizedText))
                {
                    sanitizedText = _sanitizer.Sanitize(sanitizedText, _config.Sanitization);
                }
                
                // Create our ChatMessage
                List<ToolCall>? toolCalls = null;
                if (collectedToolCalls.Any())
                {
                    toolCalls = collectedToolCalls.Select(tc => new ToolCall
                    {
                        Id = tc.Id,
                        FunctionName = tc.Name,
                        Arguments = tc.ArgumentsJson
                    }).ToList();
                }
                
                var ourMessage = new ChatMessage(ChatRole.Assistant, sanitizedText ?? combinedText, toolCalls);
                _historyManager.AddMessage(ourMessage);
                lastResponse = ourMessage;
                
                // Fire LLM response event
                _observer?.OnLlmResponse(new LlmResponseEvent(
                    BuildEventContext(),
                    combinedText,
                    collectedToolCalls,
                    usage,
                    finishReason,
                    llmCallStartTime.Elapsed));
                
                // No tool calls? Done
                if (ourMessage.ToolCalls == null || !ourMessage.ToolCalls.Any())
                {
                    break;
                }
                
                llmCallCount++;
                
                // Execute each tool call
                foreach (var toolCall in ourMessage.ToolCalls)
                {
                    // Check if this is a UI tool - if so, pause and return
                    if (_uiToolNames.Contains(toolCall.FunctionName))
                    {
                        // Return the current response - it contains the UI tool call
                        // The host will handle the UI and call SendAsync again with the result
                        var uiToolTurn = new AgentTurn
                        {
                            Response = lastResponse?.Text ?? "",
                            LlmCallsExecuted = llmCallCount,
                            Success = true
                        };
                        
                        _observer?.OnTurnComplete(new TurnCompleteEvent(
                            BuildEventContext(),
                            uiToolTurn,
                            turnStartTime.Elapsed));
                        
                        return uiToolTurn;
                    }
                    
                    var result = await ExecuteToolAsync(toolCall.FunctionName, toolCall.Arguments, ct);
                    
                    // Check for completion signal
                    if (toolCall.FunctionName == "complete_task")
                    {
                        completionSignal = result;
                    }
                    
                    // Create tool result message
                    var toolMessage = new ChatMessage(ChatRole.Tool, result, toolCall.Id);
                    
                    _historyManager.AddMessage(toolMessage);
                }
                
                // Safety: prevent infinite loops
                if (llmCallCount >= _config.MaxToolRoundsPerTurn)
                {
                    break;
                }
            }
            
            var successTurn = new AgentTurn
            {
                Response = lastResponse?.Text ?? "",
                LlmCallsExecuted = llmCallCount,
                CompletionSignal = completionSignal,
                Success = true
            };
            
            _observer?.OnTurnComplete(new TurnCompleteEvent(
                BuildEventContext(),
                successTurn,
                turnStartTime.Elapsed));
            
            return successTurn;
        }
        catch (Exception ex)
        {
            // Fire error event
            _observer?.OnError(new ErrorEvent(
                BuildEventContext(),
                ex,
                "ExecuteTurnAsync"));
            
            // Get more detailed error information
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" | Inner: {ex.InnerException.Message}";
            }
            
            return new AgentTurn
            {
                Response = "",
                Success = false,
                Error = errorMessage
            };
        }
    }
    
    private async Task<string> ExecuteToolAsync(string toolName, string argsJson, CancellationToken ct)
    {
        var toolStartTime = Stopwatch.StartNew();
        
        try
        {
            var tool = _tools.FirstOrDefault(t => t.Name == toolName);
            if (tool == null)
            {
                return $"Error: Tool '{toolName}' not found";
            }
            
            // Fire tool execution start event
            _observer?.OnToolExecutionStart(new ToolExecutionStartEvent(
                BuildEventContext(),
                toolName,
                argsJson));
            
            var result = await tool.InvokeAsync(argsJson, ct);
            
            toolStartTime.Stop();
            
            // Fire tool execution complete event
            _observer?.OnToolExecutionComplete(new ToolExecutionCompleteEvent(
                BuildEventContext(),
                toolName,
                result,
                toolStartTime.Elapsed));
            
            return result;
        }
        catch (Exception ex)
        {
            toolStartTime.Stop();
            
            // Fire tool execution complete event with error
            _observer?.OnToolExecutionComplete(new ToolExecutionCompleteEvent(
                BuildEventContext(),
                toolName,
                $"Error executing tool: {ex.Message}",
                toolStartTime.Elapsed,
                ex));
            
            return $"Error executing tool: {ex.Message}";
        }
    }
    
    private List<LlmMessage> ConvertToLlmMessages(List<ChatMessage> history)
    {
        var result = new List<LlmMessage>();
        
        foreach (var msg in history)
        {
            var role = msg.Role switch
            {
                ChatRole.System => MessageRole.System,
                ChatRole.User => MessageRole.User,
                ChatRole.Assistant => MessageRole.Assistant,
                ChatRole.Tool => MessageRole.Tool,
                _ => MessageRole.User
            };
            
            // Convert to LlmMessage - much simpler now!
            List<IMessageContent>? contents = null;
            
            if (msg.Contents?.Any() == true)
            {
                // Has rich content - convert each content type
                contents = msg.Contents.ToList(); // Already our IMessageContent type!
            }
            else if (msg.ToolCalls?.Any() == true)
            {
                // Assistant with tool calls
                contents = new List<IMessageContent>();
                
                if (!string.IsNullOrEmpty(msg.Text))
                {
                    contents.Add(new TextMessageContent(msg.Text));
                }
                
                foreach (var tc in msg.ToolCalls)
                {
                    contents.Add(new ToolCallMessageContent(tc.Id, tc.FunctionName, tc.Arguments));
                }
            }
            else if (msg.Role == ChatRole.Tool && !string.IsNullOrEmpty(msg.ToolCallId))
            {
                // Tool result message - convert to ToolResultMessageContent for proper API formatting
                contents = new List<IMessageContent>
                {
                    new ToolResultMessageContent(msg.ToolCallId, msg.Text ?? "", false)
                };
            }
            
            result.Add(new LlmMessage
            {
                Role = role,
                Text = msg.Text,
                Contents = contents,
                ToolCallId = msg.ToolCallId
            });
        }
        
        return result;
    }
    
    public IHistoryManager GetHistoryManager() => _historyManager;
    
    public IHistorySelector GetHistorySelector() => _historySelector;
    
    /// <summary>
    /// [OBSOLETE] Use config.ToolResults instead.
    /// </summary>
    [Obsolete("Use config.ToolResults instead. This will be removed in a future version.")]
    public ToolResultConfig GetRetentionConfig() => _config.ToolResults;
    
    /// <summary>
    /// Build event context for observer events
    /// </summary>
    private AgentEventContext BuildEventContext()
    {
        return new AgentEventContext
        {
            Timestamp = DateTime.UtcNow,
            ConversationId = _conversationId,
            MessageCount = _historyManager.GetHistory().Count
        };
    }
}

