using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Anthropic.Converters;
using NovaCore.AgentKit.Providers.Anthropic.Models;

namespace NovaCore.AgentKit.Providers.Anthropic;

/// <summary>
/// Anthropic chat client implementing ILlmClient using direct REST API calls
/// </summary>
public class AnthropicChatClient : ILlmClient
{
    private readonly AnthropicRestClient _restClient;
    private readonly AnthropicOptions _options;
    private readonly ILogger? _logger;
    
    public AnthropicChatClient(
        AnthropicOptions options,
        ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        
        _restClient = new AnthropicRestClient(
            apiKey: options.ApiKey,
            baseUrl: options.BaseUrl,
            timeout: options.Timeout,
            logger: logger);
    }
    
    public async Task<LlmResponse> GetResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting response from Anthropic API with model {Model}", _options.Model);
        
        try
        {
            // Convert messages to Anthropic format
            var (anthropicMessages, systemPrompt) = MessageConverter.ConvertFromLlmMessages(messages);
            
            // Build request
            var request = BuildRequest(anthropicMessages, systemPrompt, options);
            
            // Call API
            var response = await _restClient.SendMessageAsync(request, cancellationToken);
            
            // Convert response
            return MessageConverter.ConvertToLlmResponse(response);
        }
        catch (AnthropicApiException ex)
        {
            _logger?.LogError(ex, "Anthropic API error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calling Anthropic API");
            throw;
        }
    }
    
    public async IAsyncEnumerable<LlmStreamingUpdate> GetStreamingResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Getting streaming response from Anthropic API with model {Model}", _options.Model);
        
        // Convert messages to Anthropic format
        var (anthropicMessages, systemPrompt) = MessageConverter.ConvertFromLlmMessages(messages);
        
        // Build request
        var request = BuildRequest(anthropicMessages, systemPrompt, options);
        
        // Track streaming state
        var contentBlockBuilder = new Dictionary<int, ContentBlockBuilder>();
        string? messageId = null;
        string? model = null;
        string? stopReason = null;
        AnthropicUsage? usage = null;
        
        // Stream events
        await foreach (var streamEvent in _restClient.SendMessageStreamingAsync(request, cancellationToken))
        {
            switch (streamEvent)
            {
                case MessageStartEvent messageStart:
                    messageId = messageStart.Message.Id;
                    model = messageStart.Message.Model;
                    usage = messageStart.Message.Usage;
                    break;
                
                case ContentBlockStartEvent blockStart:
                    // Initialize content block builder
                    var builder = new ContentBlockBuilder();
                    
                    // Parse content block to detect if it's a tool use
                    if (blockStart.ContentBlock is JsonElement jsonElement)
                    {
                        if (jsonElement.TryGetProperty("type", out var typeElement) && 
                            typeElement.GetString() == "tool_use")
                        {
                            if (jsonElement.TryGetProperty("id", out var idElement))
                            {
                                builder.ToolCallId = idElement.GetString();
                            }
                            if (jsonElement.TryGetProperty("name", out var nameElement))
                            {
                                builder.ToolName = nameElement.GetString();
                            }
                        }
                    }
                    
                    contentBlockBuilder[blockStart.Index] = builder;
                    break;
                
                case ContentBlockDeltaEvent blockDelta:
                    // Accumulate content
                    if (contentBlockBuilder.TryGetValue(blockDelta.Index, out var deltaBuilder))
                    {
                        if (blockDelta.Delta is TextDelta textDelta)
                        {
                            deltaBuilder.AppendText(textDelta.Text);
                            
                            // Yield incremental update
                            yield return new LlmStreamingUpdate
                            {
                                TextDelta = textDelta.Text
                            };
                        }
                        else if (blockDelta.Delta is InputJsonDelta jsonDelta)
                        {
                            deltaBuilder.AppendToolJson(jsonDelta.PartialJson);
                        }
                    }
                    break;
                
                case ContentBlockStopEvent blockStop:
                    // Content block finished
                    if (contentBlockBuilder.TryGetValue(blockStop.Index, out var finishedBuilder))
                    {
                        // If it's a tool use block, yield it now
                        if (finishedBuilder.ToolCallId != null && finishedBuilder.ToolName != null)
                        {
                            yield return new LlmStreamingUpdate
                            {
                                ToolCall = new LlmToolCall
                                {
                                    Id = finishedBuilder.ToolCallId,
                                    Name = finishedBuilder.ToolName,
                                    ArgumentsJson = finishedBuilder.ToolJson ?? "{}"
                                }
                            };
                        }
                    }
                    break;
                
                case MessageDeltaEvent messageDelta:
                    stopReason = messageDelta.Delta.StopReason;
                    usage = messageDelta.Usage;
                    break;
                
                case MessageStopEvent:
                    // Stream finished
                    break;
                
                case PingEvent:
                    // Keepalive, ignore
                    break;
                
                case Models.ErrorEvent errorEvent:
                    _logger?.LogError("Streaming error: {ErrorType} - {Message}", 
                        errorEvent.Error.Type, errorEvent.Error.Message);
                    throw new AnthropicApiException(errorEvent.Error.Message, errorEvent.Error.Type);
            }
        }
        
        // Yield final update with usage and finish reason
        if (usage != null || stopReason != null)
        {
            yield return new LlmStreamingUpdate
            {
                Usage = usage != null ? new LlmUsage
                {
                    InputTokens = usage.InputTokens,
                    OutputTokens = usage.OutputTokens
                } : null,
                FinishReason = stopReason switch
                {
                    "end_turn" => LlmFinishReason.Stop,
                    "max_tokens" => LlmFinishReason.Length,
                    "tool_use" => LlmFinishReason.ToolCalls,
                    "stop_sequence" => LlmFinishReason.Stop,
                    _ => null
                }
            };
        }
    }
    
    private AnthropicRequest BuildRequest(
        List<AnthropicMessage> messages,
        string? systemPrompt,
        LlmOptions? options)
    {
        var request = new AnthropicRequest
        {
            Model = _options.Model,
            Messages = messages,
            MaxTokens = options?.MaxTokens ?? _options.MaxTokens
        };
        
        // System prompt
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            request.System = systemPrompt;
        }
        
        // Temperature
        if (options?.Temperature != null)
        {
            request.Temperature = options.Temperature.Value;
        }
        else if (_options.Temperature != 1.0)
        {
            request.Temperature = _options.Temperature;
        }
        
        // Top P
        if (options?.TopP != null)
        {
            request.TopP = options.TopP.Value;
        }
        else if (_options.TopP.HasValue)
        {
            request.TopP = _options.TopP.Value;
        }
        
        // Top K (Anthropic-specific)
        if (_options.TopK.HasValue)
        {
            request.TopK = _options.TopK.Value;
        }
        
        // Stop sequences
        if (options?.StopSequences?.Count > 0)
        {
            request.StopSequences = options.StopSequences.ToList();
        }
        
        // Tools - now a Dictionary<string, LlmTool>
        if (options?.Tools?.Count > 0)
        {
            request.Tools = MessageConverter.ConvertToAnthropicTools(options.Tools);
            
            _logger?.LogDebug("Added {Count} tools to request", request.Tools.Count);
        }
        
        return request;
    }
    
    private IDictionary<string, object?> ParseToolArguments(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object?>();
        }
        
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) 
                   ?? new Dictionary<string, object?>();
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse tool arguments: {Json}", json);
            return new Dictionary<string, object?>();
        }
    }
    
    public TService? GetService<TService>(object? key = null) where TService : class
    {
        return this is TService service ? service : null;
    }
    
    public object? GetService(Type serviceType, object? key = null)
    {
        return serviceType.IsInstanceOfType(this) ? this : null;
    }
    
    public void Dispose()
    {
        _restClient?.Dispose();
    }
}

/// <summary>
/// Helper class to build content blocks during streaming
/// </summary>
internal class ContentBlockBuilder
{
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public string ToolJson { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    
    public void AppendText(string text)
    {
        Text += text;
    }
    
    public void AppendToolJson(string json)
    {
        ToolJson += json;
    }
    
    public void SetToolInfo(string id, string name)
    {
        ToolCallId = id;
        ToolName = name;
    }
}
