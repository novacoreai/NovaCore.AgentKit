using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.XAI;

/// <summary>
/// XAI client implementing ILlmClient using direct REST API calls
/// </summary>
public class XAILlmClient : ILlmClient
{
    private readonly XAIRestClient _restClient;
    private readonly XAIOptions _options;
    private readonly ILogger? _logger;
    
    public XAILlmClient(
        XAIOptions options,
        ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        
        _restClient = new XAIRestClient(
            apiKey: options.ApiKey,
            timeout: options.Timeout,
            logger: logger);
    }
    
    public async Task<LlmResponse> GetResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling XAI API with model {Model}", _options.Model);
        
        try
        {
            var request = BuildRequest(messages, options);
            var response = await _restClient.CreateChatCompletionAsync(request, cancellationToken);
            
            return XAIResponseConverter.ConvertToLlmResponse(response);
        }
        catch (XAIApiException ex)
        {
            _logger?.LogError(ex, "XAI API error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calling XAI API");
            throw;
        }
    }
    
    public async IAsyncEnumerable<LlmStreamingUpdate> GetStreamingResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling XAI API (streaming) with model {Model}", _options.Model);
        
        var request = BuildRequest(messages, options);
        
        // Add stream_options to include usage in streaming response
        request = request with
        {
            StreamOptions = new { include_usage = true }
        };
        
        await foreach (var update in XAIResponseConverter.StreamResponseAsync(_restClient, request, cancellationToken))
        {
            yield return update;
        }
    }
    
    private XAIRequest BuildRequest(List<LlmMessage> messages, LlmOptions? options)
    {
        var XAIMessages = XAIMessageConverter.ConvertToXAIMessages(messages);
        
        var request = new XAIRequest
        {
            Model = _options.Model,
            Messages = XAIMessages,
            Temperature = options?.Temperature ?? _options.Temperature,
            TopP = options?.TopP ?? _options.TopP,
            MaxTokens = options?.MaxTokens ?? _options.MaxTokens,
            Stop = options?.StopSequences,
            FrequencyPenalty = _options.FrequencyPenalty,
            PresencePenalty = _options.PresencePenalty,
            Seed = _options.Seed
        };
        
        // Add tools if any
        if (options?.Tools != null && options.Tools.Any())
        {
            request = request with
            {
                Tools = XAIMessageConverter.ConvertToXAITools(options.Tools),
                ParallelToolCalls = true
            };
        }
        
        return request;
    }
}

