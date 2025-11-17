using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NovaCore.AgentKit.Providers.OpenAI;

/// <summary>
/// Direct REST client for OpenAI API
/// </summary>
internal class OpenAIRestClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string? _organizationId;
    private readonly ILogger? _logger;
    private const string DefaultBaseUrl = "https://api.openai.com/v1/";
    
    public OpenAIRestClient(
        string apiKey,
        string? baseUrl = null,
        string? organizationId = null,
        TimeSpan? timeout = null,
        ILogger? logger = null)
    {
        _apiKey = apiKey;
        _organizationId = organizationId;
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl ?? DefaultBaseUrl),
            Timeout = timeout ?? TimeSpan.FromMinutes(2)
        };
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        
        if (!string.IsNullOrEmpty(_organizationId))
        {
            _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", _organizationId);
        }
    }
    
    public async Task<OpenAIResponse> CreateChatCompletionAsync(
        OpenAIRequest request,
        CancellationToken cancellationToken = default)
    {
        // Use Chat Completions API (still supported by OpenAI)
        _logger?.LogDebug("Calling OpenAI API endpoint: {Endpoint}", "chat/completions");
        _logger?.LogDebug("Request model: {Model}, messages: {Count}", request.Model, request.Messages.Count);
        
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError("OpenAI API error ({StatusCode}): {Error}", response.StatusCode, error);
            _logger?.LogError("Request URL was: {RequestUri}", response.RequestMessage?.RequestUri);
            throw new OpenAIApiException($"OpenAI API error ({response.StatusCode}): {error}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken);
        return result ?? throw new OpenAIApiException("Empty response from OpenAI API");
    }
    
    public async IAsyncEnumerable<string> CreateChatCompletionStreamAsync(
        OpenAIRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request = request with { Stream = true };
        
        // Use Chat Completions API (still supported by OpenAI)
        _logger?.LogDebug("Calling OpenAI API (streaming) endpoint: {Endpoint}", "chat/completions");
        _logger?.LogDebug("Request model: {Model}, messages: {Count}", request.Model, request.Messages.Count);
        
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        
        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError("OpenAI API streaming error ({StatusCode}): {Error}", response.StatusCode, error);
            _logger?.LogError("Request URL was: {RequestUri}", httpRequest.RequestUri);
            _logger?.LogError("Full URL: {FullUri}", response.RequestMessage?.RequestUri);
            throw new OpenAIApiException($"OpenAI API error ({response.StatusCode}): {error}");
        }
        
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6); // Remove "data: " prefix
                
                if (data == "[DONE]")
                    break;
                
                yield return data;
            }
        }
    }
}

/// <summary>
/// Exception thrown when OpenAI API returns an error
/// </summary>
public class OpenAIApiException : Exception
{
    public OpenAIApiException(string message) : base(message) { }
    public OpenAIApiException(string message, Exception innerException) : base(message, innerException) { }
}

// Request/Response DTOs
internal record OpenAIRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }
    
    [JsonPropertyName("messages")]
    public required List<OpenAIMessage> Messages { get; init; }
    
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; }
    
    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopP { get; init; }
    
    [JsonPropertyName("max_completion_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxCompletionTokens { get; init; }
    
    [JsonPropertyName("stop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Stop { get; init; }
    
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenAITool>? Tools { get; init; }
    
    [JsonPropertyName("stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stream { get; init; }
    
    [JsonPropertyName("stream_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? StreamOptions { get; init; }
    
    [JsonPropertyName("reasoning_effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningEffort { get; init; }
    
    [JsonPropertyName("prompt_cache_retention")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PromptCacheRetention { get; init; }
}

internal record OpenAIMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }
    
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Content { get; init; } // Can be string or array of content parts
    
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenAIToolCall>? ToolCalls { get; init; }
    
    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }
}

internal record OpenAITool
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";
    
    [JsonPropertyName("function")]
    public required OpenAIFunction Function { get; init; }
}

internal record OpenAIFunction
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
    
    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Parameters { get; init; } // JSON schema object
}

internal record OpenAIToolCall
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";
    
    [JsonPropertyName("function")]
    public required OpenAIFunctionCall Function { get; init; }
}

internal record OpenAIFunctionCall
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; } // JSON string
}

internal record OpenAIResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    
    [JsonPropertyName("choices")]
    public List<OpenAIChoice> Choices { get; init; } = new();
    
    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIUsage? Usage { get; init; }
    
    [JsonPropertyName("model")]
    public string Model { get; init; } = "";
}

internal record OpenAIChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }
    
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIMessage? Message { get; init; }
    
    [JsonPropertyName("delta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIDelta? Delta { get; init; }
    
    [JsonPropertyName("finish_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FinishReason { get; init; }
}

internal record OpenAIDelta
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }
    
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }
    
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpenAIToolCallDelta>? ToolCalls { get; init; }
}

internal record OpenAIToolCallDelta
{
    [JsonPropertyName("index")]
    public int Index { get; init; }
    
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
    
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }
    
    [JsonPropertyName("function")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIFunctionCallDelta? Function { get; init; }
}

internal record OpenAIFunctionCallDelta
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
    
    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Arguments { get; init; } // Partial JSON string
}

internal record OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }
    
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }
    
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}

internal record OpenAIStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    
    [JsonPropertyName("choices")]
    public List<OpenAIChoice> Choices { get; init; } = new();
    
    [JsonPropertyName("model")]
    public string Model { get; init; } = "";
    
    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIUsage? Usage { get; init; }
}

