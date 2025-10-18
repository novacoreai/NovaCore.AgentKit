using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.Groq;

/// <summary>
/// Groq client implementing ILlmClient using direct REST API calls
/// </summary>
public class GroqLlmClient : ILlmClient
{
    private readonly GroqRestClient _restClient;
    private readonly GroqOptions _options;
    private readonly ILogger? _logger;
    
    public GroqLlmClient(
        GroqOptions options,
        ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        
        _restClient = new GroqRestClient(
            apiKey: options.ApiKey,
            timeout: options.Timeout,
            logger: logger);
    }
    
    public async Task<LlmResponse> GetResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling Groq API with model {Model}", _options.Model);
        
        try
        {
            var request = BuildRequest(messages, options);
            var response = await _restClient.CreateChatCompletionAsync(request, cancellationToken);
            
            return GroqResponseConverter.ConvertToLlmResponse(response);
        }
        catch (GroqApiException ex)
        {
            _logger?.LogError(ex, "Groq API error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calling Groq API");
            throw;
        }
    }
    
    public async IAsyncEnumerable<LlmStreamingUpdate> GetStreamingResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling Groq API (streaming) with model {Model}", _options.Model);
        
        var request = BuildRequest(messages, options);
        
        // Add stream_options to include usage in streaming response
        request = request with
        {
            StreamOptions = new { include_usage = true }
        };
        
        await foreach (var update in GroqResponseConverter.StreamResponseAsync(_restClient, request, cancellationToken))
        {
            yield return update;
        }
    }
    
    private GroqRequest BuildRequest(List<LlmMessage> messages, LlmOptions? options)
    {
        var GroqMessages = GroqMessageConverter.ConvertToGroqMessages(messages);
        
        var request = new GroqRequest
        {
            Model = _options.Model,
            Messages = GroqMessages,
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
                Tools = GroqMessageConverter.ConvertToGroqTools(options.Tools),
                ParallelToolCalls = true
            };
        }
        
        return request;
    }
}

