using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.OpenRouter;

/// <summary>
/// OpenRouter client implementing ILlmClient using direct REST API calls
/// </summary>
public class OpenRouterLlmClient : ILlmClient
{
    private readonly OpenRouterRestClient _restClient;
    private readonly OpenRouterOptions _options;
    private readonly ILogger? _logger;
    
    public OpenRouterLlmClient(
        OpenRouterOptions options,
        ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        
        _restClient = new OpenRouterRestClient(
            apiKey: options.ApiKey,
            timeout: options.Timeout,
            logger: logger);
    }
    
    public async Task<LlmResponse> GetResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling OpenRouter API with model {Model}", _options.Model);
        
        try
        {
            var request = BuildRequest(messages, options);
            var response = await _restClient.CreateChatCompletionAsync(request, cancellationToken);
            
            return OpenRouterResponseConverter.ConvertToLlmResponse(response);
        }
        catch (OpenRouterApiException ex)
        {
            _logger?.LogError(ex, "OpenRouter API error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calling OpenRouter API");
            throw;
        }
    }
    
    public async IAsyncEnumerable<LlmStreamingUpdate> GetStreamingResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling OpenRouter API (streaming) with model {Model}", _options.Model);
        
        var request = BuildRequest(messages, options);
        
        // Add stream_options to include usage in streaming response
        request = request with
        {
            StreamOptions = new { include_usage = true }
        };
        
        await foreach (var update in OpenRouterResponseConverter.StreamResponseAsync(_restClient, request, cancellationToken))
        {
            yield return update;
        }
    }
    
    private OpenRouterRequest BuildRequest(List<LlmMessage> messages, LlmOptions? options)
    {
        var OpenRouterMessages = OpenRouterMessageConverter.ConvertToOpenRouterMessages(messages);
        
        var request = new OpenRouterRequest
        {
            Model = _options.Model,
            Messages = OpenRouterMessages,
            Temperature = options?.Temperature ?? _options.Temperature,
            TopP = options?.TopP ?? _options.TopP,
            MaxTokens = options?.MaxTokens ?? _options.MaxTokens,
            Stop = options?.StopSequences
        };
        
        // Add tools if any
        if (options?.Tools != null && options.Tools.Any())
        {
            request = request with
            {
                Tools = OpenRouterMessageConverter.ConvertToOpenRouterTools(options.Tools)
            };
        }
        
        return request;
    }
}

