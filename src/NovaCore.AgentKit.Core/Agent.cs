using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger? _logger;
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
        ILogger? logger = null)
    {
        _llmClient = llmClient;
        _tools = tools;
        _uiToolNames = new HashSet<string>(uiTools.Select(t => t.Name));
        _historyManager = historyManager;
        _historySelector = historySelector;
        _turnValidator = turnValidator;
        _sanitizer = sanitizer;
        _config = config;
        _logger = logger;
        
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
        try
        {
            // Log user input if configured
            var loggedInput = ApplyVerbosity(userMessage, _config.Logging.LogUserInput);
            if (loggedInput != null)
            {
                LogTurnInfo("User Input", ("content", loggedInput), ("hasFiles", files?.Count > 0));
            }
            
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
                    _logger?.LogWarning("Invalid conversation turns: {Errors}", string.Join(", ", validation.Errors));
                    var fixedHistory = _turnValidator.Fix(_historyManager.GetHistory());
                    _historyManager.ReplaceHistory(fixedHistory);
                }
            }
            
            int toolCallRound = 0;
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
                
                _logger?.LogDebug("Sending {Count} tools to LLM", llmOptions.Tools.Count);
                
                // Call LLM - collect streaming updates
                var textParts = new List<string>();
                var collectedToolCalls = new List<LlmToolCall>();
                
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
                }
                
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
                
                // Log agent output if configured
                var loggedOutput = ApplyVerbosity(sanitizedText, _config.Logging.LogAgentOutput);
                if (loggedOutput != null)
                {
                    LogTurnInfo("Agent Output",
                        ("content", loggedOutput),
                        ("hasToolCalls", ourMessage.ToolCalls?.Any() == true));
                }
                
                // No tool calls? Done
                if (ourMessage.ToolCalls == null || !ourMessage.ToolCalls.Any())
                {
                    break;
                }
                
                toolCallRound++;
                
                // Execute each tool call
                foreach (var toolCall in ourMessage.ToolCalls)
                {
                    // Check if this is a UI tool - if so, pause and return
                    if (_uiToolNames.Contains(toolCall.FunctionName))
                    {
                        _logger?.LogDebug("UI tool '{ToolName}' detected - pausing execution", toolCall.FunctionName);
                        
                        // Return the current response - it contains the UI tool call
                        // The host will handle the UI and call SendAsync again with the result
                        return new AgentTurn
                        {
                            Response = lastResponse?.Text ?? "",
                            ToolCallsExecuted = toolCallRound,
                            Success = true
                        };
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
                if (toolCallRound >= _config.MaxToolRoundsPerTurn)
                {
                    _logger?.LogWarning("Max tool rounds ({Max}) reached in single turn", _config.MaxToolRoundsPerTurn);
                    break;
                }
            }
            
            return new AgentTurn
            {
                Response = lastResponse?.Text ?? "",
                ToolCallsExecuted = toolCallRound,
                CompletionSignal = completionSignal,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing agent turn");
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
        try
        {
            _logger?.LogDebug("Executing tool: {Tool}", toolName);
            
            var tool = _tools.FirstOrDefault(t => t.Name == toolName);
            if (tool == null)
            {
                return $"Error: Tool '{toolName}' not found";
            }
            
            // Log tool call request if configured
            var loggedRequest = ApplyVerbosity(argsJson, _config.Logging.LogToolCallRequests);
            if (loggedRequest != null)
            {
                // Format JSON for better log readability (unescapes unicode)
                var formattedArgs = JsonHelper.FormatForLogging(loggedRequest);
                LogTurnInfo("Tool Call Request",
                    ("toolName", toolName),
                    ("arguments", formattedArgs));
            }
            
            var result = await tool.InvokeAsync(argsJson, ct);
            
            // Log tool call response if configured
            var loggedResponse = ApplyVerbosity(result, _config.Logging.LogToolCallResponses);
            if (loggedResponse != null)
            {
                // Format JSON for better log readability (unescapes unicode)
                var formattedResult = JsonHelper.FormatForLogging(loggedResponse);
                LogTurnInfo("Tool Call Response",
                    ("toolName", toolName),
                    ("result", formattedResult));
            }
            
            _logger?.LogDebug("Tool {Tool} executed successfully", toolName);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing tool {Tool}", toolName);
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
    /// Helper method to apply truncation based on verbosity setting
    /// </summary>
    private string? ApplyVerbosity(string? content, LogVerbosity verbosity)
    {
        if (content == null || verbosity == LogVerbosity.None)
        {
            return null;
        }
        
        if (verbosity == LogVerbosity.Full)
        {
            return content;
        }
        
        // Truncated
        if (content.Length <= _config.Logging.TruncationLength)
        {
            return content;
        }
        
        return content.Substring(0, _config.Logging.TruncationLength) + "...";
    }
    
    /// <summary>
    /// Log with structured or simple format based on config
    /// </summary>
    private void LogTurnInfo(string message, params (string Key, object? Value)[] properties)
    {
        if (_logger == null) return;
        
        if (_config.Logging.UseStructuredLogging)
        {
            // Build structured log with properties
            var state = properties.ToDictionary(p => p.Key, p => p.Value);
            _logger.LogInformation("[Turn] {Message} {@Properties}", message, state);
        }
        else
        {
            // Simple text format
            var propsText = string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}"));
            _logger.LogInformation("[Turn] {Message} | {Properties}", message, propsText);
        }
    }
}

