using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Providers.Anthropic.Models;

namespace NovaCore.AgentKit.Providers.Anthropic;

/// <summary>
/// HTTP client for direct communication with Anthropic Messages API
/// </summary>
public class AnthropicRestClient : IDisposable
{
    private const string DefaultBaseUrl = "https://api.anthropic.com/v1/";
    private const string DefaultApiVersion = "2023-06-01";
    
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiVersion;
    private readonly ILogger? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public AnthropicRestClient(
        string apiKey,
        string? baseUrl = null,
        string? apiVersion = null,
        TimeSpan? timeout = null,
        ILogger? logger = null)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _apiVersion = apiVersion ?? DefaultApiVersion;
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl ?? DefaultBaseUrl),
            Timeout = timeout ?? TimeSpan.FromMinutes(2)
        };
        
        // Configure JSON options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }
    
    /// <summary>
    /// Send a message request to the Anthropic API
    /// </summary>
    public async Task<AnthropicResponse> SendMessageAsync(
        AnthropicRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Sending message request to Anthropic API");
        
        using var httpRequest = CreateHttpRequest(request);
        
        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(response, cancellationToken);
            }
            
            var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(_jsonOptions, cancellationToken);
            
            if (result == null)
            {
                throw new AnthropicApiException("Failed to deserialize response from Anthropic API");
            }
            
            _logger?.LogDebug("Received response from Anthropic API: {MessageId}", result.Id);
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error calling Anthropic API");
            throw new AnthropicApiException("HTTP error calling Anthropic API", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogError(ex, "Request to Anthropic API timed out");
            throw new AnthropicApiException("Request timed out", ex);
        }
    }
    
    /// <summary>
    /// Send a streaming message request to the Anthropic API
    /// </summary>
    public async IAsyncEnumerable<AnthropicStreamingEvent> SendMessageStreamingAsync(
        AnthropicRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Sending streaming message request to Anthropic API");
        
        // Ensure stream is enabled
        request.Stream = true;
        
        using var httpRequest = CreateHttpRequest(request);
        
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest, 
                HttpCompletionOption.ResponseHeadersRead, 
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(response, cancellationToken);
            }
            
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(':'))
                {
                    continue;
                }
                
                // Parse SSE format: "event: <type>" and "data: <json>"
                if (line.StartsWith("event:"))
                {
                    // Event type line - we'll get the data on the next line
                    continue;
                }
                
                if (line.StartsWith("data:"))
                {
                    var jsonData = line.Substring(5).Trim();
                    
                    // Parse the event
                    AnthropicStreamingEvent? streamEvent;
                    try
                    {
                        streamEvent = JsonSerializer.Deserialize<AnthropicStreamingEvent>(jsonData, _jsonOptions);
                    }
                    catch (JsonException ex)
                    {
                        _logger?.LogWarning(ex, "Failed to parse streaming event: {Data}", jsonData);
                        continue;
                    }
                    
                    if (streamEvent != null)
                    {
                        yield return streamEvent;
                        
                        // Check for error events
                        if (streamEvent is ErrorEvent errorEvent)
                        {
                            throw new AnthropicApiException(
                                $"Streaming error: {errorEvent.Error.Message}",
                                errorEvent.Error.Type);
                        }
                    }
                }
            }
        }
        finally
        {
            response?.Dispose();
        }
    }
    
    private HttpRequestMessage CreateHttpRequest(AnthropicRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "messages");
        
        // Add required headers
        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", _apiVersion);
        
        // Add beta headers if needed (for extended thinking, etc.)
        // This can be extended based on options
        
        // Serialize request body
        var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
        httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        
        _logger?.LogTrace("Request body: {Body}", jsonContent);
        
        return httpRequest;
    }
    
    private async Task HandleErrorResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        _logger?.LogError("Anthropic API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
        
        try
        {
            var errorResponse = JsonSerializer.Deserialize<AnthropicErrorResponse>(errorContent, _jsonOptions);
            if (errorResponse?.Error != null)
            {
                throw new AnthropicApiException(
                    $"Anthropic API error: {errorResponse.Error.Message}",
                    errorResponse.Error.Type,
                    (int)response.StatusCode);
            }
        }
        catch (JsonException)
        {
            // Could not parse error response
        }
        
        throw new AnthropicApiException(
            $"Anthropic API returned error status: {response.StatusCode}",
            statusCode: (int)response.StatusCode);
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Exception thrown when Anthropic API returns an error
/// </summary>
public class AnthropicApiException : Exception
{
    public string? ErrorType { get; }
    public int? StatusCode { get; }
    
    public AnthropicApiException(string message) : base(message)
    {
    }
    
    public AnthropicApiException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
    
    public AnthropicApiException(string message, string? errorType = null, int? statusCode = null) 
        : base(message)
    {
        ErrorType = errorType;
        StatusCode = statusCode;
    }
}


