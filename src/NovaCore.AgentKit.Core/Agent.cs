using System.Diagnostics;
using System.Text.Json;
using NovaCore.AgentKit.Core.CostTracking;
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
    private readonly string _modelName;
    private readonly ICostCalculator _costCalculator;
    private readonly List<ToolRegistration> _toolRegistrations;
    private readonly HashSet<string> _uiToolNames;
    private readonly IHistoryManager _historyManager;
    private readonly IHistorySelector _historySelector;
    private readonly ITurnValidator _turnValidator;
    private readonly IOutputSanitizer _sanitizer;
    private readonly AgentConfig _config;
    private readonly IAgentObserver? _observer;
    private readonly string? _conversationId;
    private readonly Dictionary<string, JsonElement> _toolSchemas; // Only tools with skipDefinition=false
    private readonly Dictionary<string, ITool> _allToolsMap; // Map of all tools (regular + UI) by name for execution
    
    public Agent(
        ILlmClient llmClient,
        string modelName,
        ICostCalculator costCalculator,
        List<ToolRegistration> toolRegistrations,
        List<ToolRegistration> uiToolRegistrations,
        IHistoryManager historyManager,
        IHistorySelector historySelector,
        ITurnValidator turnValidator,
        IOutputSanitizer sanitizer,
        AgentConfig config,
        IAgentObserver? observer = null,
        string? conversationId = null)
    {
        _llmClient = llmClient;
        _modelName = modelName;
        _costCalculator = costCalculator;
        _toolRegistrations = toolRegistrations;
        _uiToolNames = new HashSet<string>(uiToolRegistrations.Select(r => r.Tool.Name));
        _historyManager = historyManager;
        _historySelector = historySelector;
        _turnValidator = turnValidator;
        _sanitizer = sanitizer;
        _config = config;
        _observer = observer;
        _conversationId = conversationId;
        
        // Build tool schemas dictionary (only for tools where skipDefinition=false)
        // and all tools map for execution (includes ALL tools regardless of skipDefinition)
        _toolSchemas = new Dictionary<string, JsonElement>();
        _allToolsMap = new Dictionary<string, ITool>();
        
        foreach (var registration in _toolRegistrations)
        {
            // Always add to execution map
            _allToolsMap[registration.Tool.Name] = registration.Tool;
            
            // Only add to schemas if not skipping definition
            if (!registration.SkipDefinition)
            {
                _toolSchemas[registration.Tool.Name] = registration.Tool.ParameterSchema.RootElement.Clone();
            }
        }
        foreach (var uiRegistration in uiToolRegistrations)
        {
            // Always add to execution map
            _allToolsMap[uiRegistration.Tool.Name] = uiRegistration.Tool;
            
            // Only add to schemas if not skipping definition
            if (!uiRegistration.SkipDefinition)
            {
                _toolSchemas[uiRegistration.Tool.Name] = uiRegistration.Tool.ParameterSchema.RootElement.Clone();
            }
        }
        
        // Add system prompt if configured with ONE tool call at a time instruction
        if (!string.IsNullOrEmpty(_config.SystemPrompt))
        {
            var history = _historyManager.GetHistory();
            if (history.Count == 0 || history[0].Role != ChatRole.System)
            {
                var enhancedPrompt = _config.SystemPrompt;
                
                // Add tool call instruction if there are tools available
                if (_toolRegistrations.Any())
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
            
            // Track cumulative tokens and cost for this turn
            int totalInputTokens = 0;
            int totalOutputTokens = 0;
            decimal totalCost = 0m;
            
            // Tool call loop
            while (true)
            {
                // Get full history from manager
                var fullHistory = _historyManager.GetHistory();
                
                // Apply tool result filtering and multimodal filtering to select context for model
                var contextHistory = _historySelector.SelectMessagesForContext(
                    fullHistory, 
                    checkpoint: null,
                    _config.ToolResults,
                    _config.MaxMultimodalMessages);
                
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
                
                // Calculate cost if usage is available
                if (usage != null)
                {
                    // Calculate input and output costs separately
                    var inputCost = _costCalculator.Calculate(_modelName, usage.InputTokens, 0);
                    var outputCost = _costCalculator.Calculate(_modelName, 0, usage.OutputTokens);
                    
                    // Create usage with cost information
                    usage = new LlmUsage
                    {
                        InputTokens = usage.InputTokens,
                        OutputTokens = usage.OutputTokens,
                        InputCost = inputCost,
                        OutputCost = outputCost
                    };
                    
                    // Accumulate totals
                    totalInputTokens += usage.InputTokens;
                    totalOutputTokens += usage.OutputTokens;
                    totalCost += usage.TotalCost;
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
                    _modelName,
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
                            Success = true,
                            TotalInputTokens = totalInputTokens,
                            TotalOutputTokens = totalOutputTokens,
                            TotalCost = totalCost
                        };
                        
                        _observer?.OnTurnComplete(new TurnCompleteEvent(
                            BuildEventContext(),
                            uiToolTurn,
                            turnStartTime.Elapsed));
                        
                        return uiToolTurn;
                    }
                    
                    // Execute tool and get result (may include additional content for multimodal tools)
                    var toolResult = await ExecuteToolWithResultAsync(toolCall.FunctionName, toolCall.Arguments, ct);
                    
                    // Check for completion signal
                    if (toolCall.FunctionName == "complete_task")
                    {
                        completionSignal = toolResult.Text;
                    }
                    
                    // Create tool result message with text result and optional additional content
                    ChatMessage toolMessage;
                    if (toolResult.AdditionalContent != null)
                    {
                        // Include additional content (e.g., screenshot) in the same tool message
                        var contents = new List<IMessageContent>
                        {
                            new TextMessageContent(toolResult.Text),
                            toolResult.AdditionalContent
                        };
                        toolMessage = new ChatMessage(ChatRole.Tool, contents, toolCall.Id);
                    }
                    else
                    {
                        toolMessage = new ChatMessage(ChatRole.Tool, toolResult.Text, toolCall.Id);
                    }
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
                Success = true,
                TotalInputTokens = totalInputTokens,
                TotalOutputTokens = totalOutputTokens,
                TotalCost = totalCost
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
                Error = errorMessage,
                TotalInputTokens = 0,
                TotalOutputTokens = 0,
                TotalCost = 0m
            };
        }
    }
    
    /// <summary>
    /// Execute a tool and return the full result including any additional multimodal content.
    /// </summary>
    private async Task<ToolResult> ExecuteToolWithResultAsync(string toolName, string argsJson, CancellationToken ct)
    {
        var toolStartTime = Stopwatch.StartNew();
        
        try
        {
            if (!_allToolsMap.TryGetValue(toolName, out var tool))
            {
                return new ToolResult { Text = $"Error: Tool '{toolName}' not found" };
            }
            
            // Fire tool execution start event
            _observer?.OnToolExecutionStart(new ToolExecutionStartEvent(
                BuildEventContext(),
                toolName,
                argsJson));
            
            ToolResult result;
            
            // Check if this is a multimodal tool
            if (tool is IMultimodalTool multimodalTool)
            {
                result = await multimodalTool.InvokeWithResultAsync(argsJson, ct);
            }
            else
            {
                // Regular tool - wrap result in ToolResult
                var textResult = await tool.InvokeAsync(argsJson, ct);
                result = new ToolResult { Text = textResult };
            }
            
            toolStartTime.Stop();
            
            // Fire tool execution complete event
            _observer?.OnToolExecutionComplete(new ToolExecutionCompleteEvent(
                BuildEventContext(),
                toolName,
                result.Text,
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
            
            return new ToolResult { Text = $"Error executing tool: {ex.Message}" };
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
                // Tool result message - use Contents if available (for multimodal tool results)
                if (msg.Contents?.Any() == true)
                {
                    // Use existing contents (includes TextMessageContent + optional ImageMessageContent)
                    // But ensure we have a ToolResultMessageContent for the text part
                    var textContent = msg.Contents.OfType<TextMessageContent>().FirstOrDefault();
                    var otherContents = msg.Contents.Where(c => c is not TextMessageContent).ToList();
                    
                    contents = new List<IMessageContent>
                    {
                        new ToolResultMessageContent(msg.ToolCallId, textContent?.Text ?? msg.Text ?? "", false)
                    };
                    contents.AddRange(otherContents);
                }
                else
                {
                    // Fallback to text-only tool result
                    contents = new List<IMessageContent>
                    {
                        new ToolResultMessageContent(msg.ToolCallId, msg.Text ?? "", false)
                    };
                }
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

