using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.OpenAI;

/// <summary>
/// OpenAI client implementing ILlmClient using direct REST API calls
/// </summary>
public class OpenAILlmClient : ILlmClient
{
    private readonly OpenAIRestClient _restClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger? _logger;
    
    public OpenAILlmClient(
        OpenAIOptions options,
        ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        
        _restClient = new OpenAIRestClient(
            apiKey: options.ApiKey,
            baseUrl: options.BaseUrl,
            organizationId: options.OrganizationId,
            timeout: options.Timeout,
            logger: logger);
    }
    
    public async Task<LlmResponse> GetResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling OpenAI API with model {Model}", _options.Model);
        
        try
        {
            var request = BuildRequest(messages, options);
            var response = await _restClient.CreateChatCompletionAsync(request, cancellationToken);
            
            return OpenAIResponseConverter.ConvertToLlmResponse(response);
        }
        catch (OpenAIApiException ex)
        {
            _logger?.LogError(ex, "OpenAI API error: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calling OpenAI API");
            throw;
        }
    }
    
    public async IAsyncEnumerable<LlmStreamingUpdate> GetStreamingResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling OpenAI API (streaming) with model {Model}", _options.Model);
        
        var request = BuildRequest(messages, options);
        
        // Add stream_options to include usage in streaming response
        request = request with
        {
            StreamOptions = new { include_usage = true }
        };
        
        await foreach (var update in OpenAIResponseConverter.StreamResponseAsync(_restClient, request, cancellationToken))
        {
            yield return update;
        }
    }
    
    private OpenAIRequest BuildRequest(List<LlmMessage> messages, LlmOptions? options)
    {
        var openAIMessages = OpenAIMessageConverter.ConvertToOpenAIMessages(messages);
        
        var request = new OpenAIRequest
        {
            Model = _options.Model,
            Messages = openAIMessages,
            Temperature = options?.Temperature ?? _options.Temperature,
            TopP = options?.TopP ?? _options.TopP,
            MaxCompletionTokens = options?.MaxTokens ?? _options.MaxTokens,
            Stop = options?.StopSequences,
            ReasoningEffort = _options.ReasoningEffort,
            PromptCacheRetention = _options.PromptCacheRetention
        };
        
        // Add tools if any
        if (options?.Tools != null && options.Tools.Any())
        {
            request = request with
            {
                Tools = OpenAIMessageConverter.ConvertToOpenAITools(options.Tools)
            };
        }
        
        return request;
    }
}

