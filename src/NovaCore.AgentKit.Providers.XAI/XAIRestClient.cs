using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NovaCore.AgentKit.Providers.XAI;

/// <summary>
/// Direct REST client for xAI API (OpenAI-compatible)
/// </summary>
internal class XAIRestClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger? _logger;
    private const string XAIBaseUrl = "https://api.x.ai/v1/";
    
    public XAIRestClient(
        string apiKey,
        TimeSpan? timeout = null,
        ILogger? logger = null)
    {
        _apiKey = apiKey;
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(XAIBaseUrl),
            Timeout = timeout ?? TimeSpan.FromMinutes(2)
        };
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }
    
    public async Task<XAIResponse> CreateChatCompletionAsync(
        XAIRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new XAIApiException($"XAI API error ({response.StatusCode}): {error}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<XAIResponse>(cancellationToken);
        return result ?? throw new XAIApiException("Empty response from XAI API");
    }
    
    public async IAsyncEnumerable<string> CreateChatCompletionStreamAsync(
        XAIRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request = request with { Stream = true };
        
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        
        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new XAIApiException($"XAI API error ({response.StatusCode}): {error}");
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
/// Exception thrown when XAI API returns an error
/// </summary>
public class XAIApiException : Exception
{
    public XAIApiException(string message) : base(message) { }
    public XAIApiException(string message, Exception innerException) : base(message, innerException) { }
}

// Request/Response DTOs (OpenAI-compatible format)
internal record XAIRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }
    
    [JsonPropertyName("messages")]
    public required List<XAIMessage> Messages { get; init; }
    
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; }
    
    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopP { get; init; }
    
    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }
    
    [JsonPropertyName("stop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Stop { get; init; }
    
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<XAITool>? Tools { get; init; }
    
    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ToolChoice { get; init; }
    
    [JsonPropertyName("parallel_tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ParallelToolCalls { get; init; }
    
    [JsonPropertyName("stream")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stream { get; init; }
    
    [JsonPropertyName("stream_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? StreamOptions { get; init; }
    
    [JsonPropertyName("store")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Store { get; init; }
    
    [JsonPropertyName("frequency_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? FrequencyPenalty { get; init; }
    
    [JsonPropertyName("presence_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PresencePenalty { get; init; }
    
    [JsonPropertyName("seed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Seed { get; init; }
    
    [JsonPropertyName("user")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? User { get; init; }
    
    [JsonPropertyName("n")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? N { get; init; }
    
    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ResponseFormat { get; init; }
}

internal record XAIMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }
    
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Content { get; init; }
    
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<XAIToolCall>? ToolCalls { get; init; }
    
    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }
    
    [JsonPropertyName("refusal")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Refusal { get; init; }
}

internal record XAITool
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";
    
    [JsonPropertyName("function")]
    public required XAIFunction Function { get; init; }
}

internal record XAIFunction
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
    
    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Parameters { get; init; }
}

internal record XAIToolCall
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";
    
    [JsonPropertyName("function")]
    public required XAIFunctionCall Function { get; init; }
}

internal record XAIFunctionCall
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }
}

internal record XAIResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    
    [JsonPropertyName("object")]
    public string Object { get; init; } = "";
    
    [JsonPropertyName("created")]
    public int Created { get; init; }
    
    [JsonPropertyName("model")]
    public string Model { get; init; } = "";
    
    [JsonPropertyName("choices")]
    public List<XAIChoice> Choices { get; init; } = new();
    
    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public XAIUsage? Usage { get; init; }
    
    [JsonPropertyName("system_fingerprint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SystemFingerprint { get; init; }
}

internal record XAIChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }
    
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public XAIMessage? Message { get; init; }
    
    [JsonPropertyName("delta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public XAIDelta? Delta { get; init; }
    
    [JsonPropertyName("finish_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FinishReason { get; init; }
}

internal record XAIDelta
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }
    
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }
    
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<XAIToolCallDelta>? ToolCalls { get; init; }
}

internal record XAIToolCallDelta
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
    public XAIFunctionCallDelta? Function { get; init; }
}

internal record XAIFunctionCallDelta
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
    
    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Arguments { get; init; }
}

internal record XAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }
    
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }
    
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
    
    [JsonPropertyName("prompt_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public XAIPromptTokensDetails? PromptTokensDetails { get; init; }
    
    [JsonPropertyName("completion_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public XAICompletionTokensDetails? CompletionTokensDetails { get; init; }
    
    [JsonPropertyName("num_sources_used")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NumSourcesUsed { get; init; }
}

internal record XAIPromptTokensDetails
{
    [JsonPropertyName("text_tokens")]
    public int TextTokens { get; init; }
    
    [JsonPropertyName("audio_tokens")]
    public int AudioTokens { get; init; }
    
    [JsonPropertyName("image_tokens")]
    public int ImageTokens { get; init; }
    
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; init; }
}

internal record XAICompletionTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; init; }
    
    [JsonPropertyName("audio_tokens")]
    public int AudioTokens { get; init; }
    
    [JsonPropertyName("accepted_prediction_tokens")]
    public int AcceptedPredictionTokens { get; init; }
    
    [JsonPropertyName("rejected_prediction_tokens")]
    public int RejectedPredictionTokens { get; init; }
}

internal record XAIStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    
    [JsonPropertyName("choices")]
    public List<XAIChoice> Choices { get; init; } = new();
    
    [JsonPropertyName("model")]
    public string Model { get; init; } = "";
    
    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public XAIUsage? Usage { get; init; }
}

