using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.Google;

/// <summary>
/// Google Gemini client implementing ILlmClient using direct HTTP API calls
/// </summary>
public class GoogleChatClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly GoogleOptions _options;
    private readonly ILogger? _logger;
    private readonly string _baseUrl = "https://generativelanguage.googleapis.com/v1beta";
    
    public GoogleChatClient(
        HttpClient httpClient,
        GoogleOptions options,
        ILogger? logger = null)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }
    
    public async Task<LlmResponse> GetResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling Google API with model {Model}", _options.Model);
        
        try
        {
            // Build request
            var request = BuildGenerateContentRequest(messages, options);
            
            var requestJson = JsonSerializer.Serialize(request);
            _logger?.LogDebug("Google API Request: {Request}", requestJson);
            
            // Call Google API
            var url = $"{_baseUrl}/models/{_options.Model}:generateContent?key={_options.ApiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            
            // Handle errors
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Google API error: {response.StatusCode} - {errorContent}");
            }
            
            // Parse response
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogDebug("Google API Raw Response: {Response}", responseText);
            
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseText);
            if (geminiResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize Google API response");
            }
            
            
            // Convert to LlmResponse
            return ConvertToLlmResponse(geminiResponse);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calling Google API");
            throw;
        }
    }
    
    public async IAsyncEnumerable<LlmStreamingUpdate> GetStreamingResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Calling Google API (streaming) with model {Model}", _options.Model);
        
        // Build and execute request (same as GetResponseAsync but return as streaming)
        var request = BuildGenerateContentRequest(messages, options);
        
        var requestJson = JsonSerializer.Serialize(request);
        _logger?.LogDebug("Google API Streaming Request: {Request}", requestJson);
        
        var url = $"{_baseUrl}/models/{_options.Model}:generateContent?key={_options.ApiKey}";
        var httpResponse = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        
        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Google API error: {httpResponse.StatusCode} - {errorContent}");
        }
        
        var responseText = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        _logger?.LogDebug("Google API Streaming Response: {Response}", responseText);
        
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseText);
        if (geminiResponse == null)
        {
            throw new InvalidOperationException("Failed to deserialize Google API response");
        }
        
        // Create streaming update
        var update = new LlmStreamingUpdate();
        
        // Extract content from first candidate and add to update
        if (geminiResponse.Candidates?.Any() == true)
        {
            var candidate = geminiResponse.Candidates[0];
            
            if (candidate.Content?.Parts != null)
            {
                foreach (var part in candidate.Content.Parts)
                {
                    if (part.Text != null)
                    {
                        update = new LlmStreamingUpdate { TextDelta = part.Text };
                        yield return update;
                    }
                    else if (part.FunctionCall != null)
                    {
                        var callId = Guid.NewGuid().ToString();
                        var argsJson = JsonSerializer.Serialize(part.FunctionCall.Args ?? new Dictionary<string, object?>());
                        
                        update = new LlmStreamingUpdate 
                        { 
                            ToolCall = new LlmToolCall
                            {
                                Id = callId,
                                Name = part.FunctionCall.Name,
                                ArgumentsJson = argsJson
                            }
                        };
                        yield return update;
                    }
                }
            }
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
        // HttpClient is managed externally
    }
    
    private object BuildGenerateContentRequest(List<LlmMessage> messages, LlmOptions? options)
    {
        // Extract system instruction
        string? systemInstruction = messages
            .FirstOrDefault(m => m.Role == MessageRole.System)?.Text;
        
        // Convert messages to Google format
        var contents = ConvertToGoogleContents(messages);
        
        // Build generation config
        var generationConfig = BuildGenerationConfig(options);
        
        // Build tools
        var tools = BuildTools(options);
        
        // Construct request object
        var request = new Dictionary<string, object>();
        
        request["contents"] = contents;
        
        if (!string.IsNullOrEmpty(systemInstruction))
        {
            request["systemInstruction"] = new
            {
                parts = new[] { new { text = systemInstruction } }
            };
        }
        
        if (generationConfig != null)
        {
            request["generationConfig"] = generationConfig;
        }
        
        if (tools != null && tools.Any())
        {
            request["tools"] = tools;
        }
        
        return request;
    }
    
    private List<object> ConvertToGoogleContents(List<LlmMessage> messages)
    {
        var contents = new List<object>();
        
        // Build a mapping of CallId -> ToolName from tool calls in the conversation
        var callIdToToolName = new Dictionary<string, string>();
        foreach (var msg in messages)
        {
            if (msg.Contents != null)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is ToolCallMessageContent toolCall)
                    {
                        callIdToToolName[toolCall.CallId] = toolCall.ToolName;
                    }
                }
            }
        }
        
        foreach (var msg in messages)
        {
            // Skip system messages (handled separately)
            if (msg.Role == MessageRole.System)
                continue;
            
            // Map role
            var role = msg.Role switch
            {
                MessageRole.Assistant => "model",
                MessageRole.User => "user",
                MessageRole.Tool => "user", // Tool results come back as user messages in Google
                _ => "user"
            };
            
            var parts = new List<object>();
            
            // Handle different content types
            if (msg.Contents != null)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is TextMessageContent textContent && !string.IsNullOrEmpty(textContent.Text))
                    {
                        parts.Add(new { text = textContent.Text });
                    }
                    else if (content is ImageMessageContent imageContent)
                    {
                        // Image or file content - convert to Google inline_data format
                        parts.Add(new
                        {
                            inline_data = new
                            {
                                mime_type = imageContent.MimeType,
                                data = Convert.ToBase64String(imageContent.Data)
                            }
                        });
                    }
                    else if (content is ToolCallMessageContent toolCallContent)
                    {
                        // Model requesting a tool call
                        Dictionary<string, object?> args;
                        
                        // Handle empty or whitespace ArgumentsJson
                        if (string.IsNullOrWhiteSpace(toolCallContent.ArgumentsJson))
                        {
                            args = new Dictionary<string, object?>();
                        }
                        else
                        {
                            args = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCallContent.ArgumentsJson)
                                       ?? new Dictionary<string, object?>();
                        }
                        
                        parts.Add(new
                        {
                            functionCall = new
                            {
                                name = toolCallContent.ToolName,
                                args = args
                            }
                        });
                    }
                    else if (content is ToolResultMessageContent toolResultContent)
                    {
                        // Tool result coming back
                        // Google expects the function name, not the call ID
                        // Look up the tool name from our mapping
                        if (!callIdToToolName.TryGetValue(toolResultContent.CallId, out var toolName))
                        {
                            // CRITICAL: CallId not found in mapping - this will cause "Function Call must be matched to Function Response" error
                            // This happens when the tool result's CallId doesn't match any previous tool call
                            throw new InvalidOperationException(
                                $"Tool result CallId '{toolResultContent.CallId}' not found in tool call mapping. " +
                                $"Available CallIds: {string.Join(", ", callIdToToolName.Keys)}. " +
                                $"This usually means the tool result message has an incorrect ToolCallId.");
                        }
                        
                        // Try to parse the result as JSON object, otherwise wrap it
                        object responseData;
                        try
                        {
                            // Try to deserialize as JSON object
                            var jsonElement = JsonSerializer.Deserialize<JsonElement>(toolResultContent.Result);
                            responseData = ConvertJsonElementToObject(jsonElement);
                        }
                        catch
                        {
                            // If not valid JSON, wrap as simple result
                            responseData = new { result = toolResultContent.Result };
                        }
                        
                        parts.Add(new
                        {
                            functionResponse = new
                            {
                                name = toolName,
                                response = responseData
                            }
                        });
                    }
                }
            }
            
            // Fall back to text if no contents
            if (!parts.Any() && !string.IsNullOrEmpty(msg.Text))
            {
                parts.Add(new { text = msg.Text });
            }
            
            if (parts.Any())
            {
                contents.Add(new
                {
                    role,
                    parts
                });
            }
        }
        
        return contents;
    }
    
    private object? BuildGenerationConfig(LlmOptions? options)
    {
        var config = new Dictionary<string, object>();
        
        if (options?.Temperature != null)
        {
            config["temperature"] = options.Temperature.Value;
        }
        else if (_options.Temperature.HasValue)
        {
            config["temperature"] = _options.Temperature.Value;
        }
        
        if (options?.MaxTokens != null)
        {
            config["maxOutputTokens"] = options.MaxTokens.Value;
        }
        else if (_options.MaxTokens != null)
        {
            config["maxOutputTokens"] = _options.MaxTokens.Value;
        }
        
        if (options?.TopP != null)
        {
            config["topP"] = options.TopP.Value;
        }
        else if (_options.TopP.HasValue)
        {
            config["topP"] = _options.TopP.Value;
        }
        
        if (_options.TopK.HasValue)
        {
            config["topK"] = _options.TopK.Value;
        }
        
        // Add thinking config if thinking level is specified
        if (_options.ThinkingLevel.HasValue)
        {
            var thinkingLevelString = _options.ThinkingLevel.Value switch
            {
                ThinkingLevel.High => "HIGH",
                ThinkingLevel.Medium => "MEDIUM",
                ThinkingLevel.Low => "LOW",
                ThinkingLevel.Minimal => "MINIMAL",
                _ => "HIGH"
            };
            
            config["thinkingConfig"] = new Dictionary<string, object>
            {
                ["thinkingLevel"] = thinkingLevelString
            };
            
            _logger?.LogDebug("Setting thinking level to {ThinkingLevel}", thinkingLevelString);
        }
        
        return config.Any() ? config : null;
    }
    
    private List<object>? BuildTools(LlmOptions? options)
    {
        var tools = new List<object>();
        
        // Add Computer Use tool if enabled
        if (_options.EnableComputerUse)
        {
            _logger?.LogDebug("Adding Computer Use tool for Google");
            
            var computerUseTool = new Dictionary<string, object>
            {
                ["computer_use"] = new Dictionary<string, object>
                {
                    ["environment"] = _options.ComputerUseEnvironment == ComputerUseEnvironment.Browser 
                        ? "ENVIRONMENT_BROWSER" 
                        : "ENVIRONMENT_DESKTOP"
                }
            };
            
            // Add excluded functions if specified
            if (_options.ExcludedComputerUseFunctions?.Any() == true)
            {
                ((Dictionary<string, object>)computerUseTool["computer_use"])["excluded_predefined_functions"] = 
                    _options.ExcludedComputerUseFunctions;
            }
            
            tools.Add(computerUseTool);
        }
        
        // Add custom function declarations if provided
        if (options?.Tools != null && options.Tools.Any())
        {
            _logger?.LogDebug("Building {Count} custom tools for Google", options.Tools.Count);
            
            var functionDeclarations = new List<object>();
            
            foreach (var kvp in options.Tools)
            {
                var tool = kvp.Value;
                
                try
                {
                    // Transform schema to be Google-compatible
                    var transformedSchema = TransformSchemaForGoogle(tool.ParameterSchema);
                    
                    // Build function declaration
                    var funcDecl = new Dictionary<string, object>
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description
                    };
                    
                    // Extract parameters
                    if (transformedSchema.TryGetProperty("properties", out var propsElement))
                    {
                        var parameters = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = ConvertJsonElementToDictionary(propsElement)
                        };
                        
                        // Add required fields
                        if (transformedSchema.TryGetProperty("required", out var requiredElement) 
                            && requiredElement.ValueKind == JsonValueKind.Array)
                        {
                            parameters["required"] = requiredElement.EnumerateArray()
                                .Where(e => e.ValueKind == JsonValueKind.String)
                                .Select(e => e.GetString()!)
                                .ToList();
                        }
                        
                        funcDecl["parameters"] = parameters;
                    }
                    
                    functionDeclarations.Add(funcDecl);
                    _logger?.LogDebug("Built custom tool {Tool} for Google", tool.Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to build tool {Tool}", tool.Name);
                    throw;
                }
            }
            
            if (functionDeclarations.Any())
            {
                tools.Add(new { functionDeclarations });
            }
        }
        
        return tools.Any() ? tools : null;
    }
    
    private Dictionary<string, object> ConvertJsonElementToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object>();
        
        foreach (var prop in element.EnumerateObject())
        {
            result[prop.Name] = ConvertJsonElementToObject(prop.Value);
        }
        
        return result;
    }
    
    private object ConvertJsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.ToString()!
        };
    }
    
    private JsonElement TransformSchemaForGoogle(JsonElement schema)
    {
        using var doc = JsonDocument.Parse(schema.GetRawText());
        var transformed = TransformSchemaElement(doc.RootElement);
        
        var json = JsonSerializer.Serialize(transformed);
        return JsonDocument.Parse(json).RootElement;
    }
    
    private object TransformSchemaElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var result = new Dictionary<string, object>();
            
            var hasTypeObject = false;
            var hasProperties = false;
            
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name == "type")
                {
                    // Handle both "type": "object" and "type": ["object", ...] (union types)
                    if (prop.Value.ValueKind == JsonValueKind.String && prop.Value.GetString() == "object")
                    {
                        hasTypeObject = true;
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var typeElement in prop.Value.EnumerateArray())
                        {
                            if (typeElement.ValueKind == JsonValueKind.String && typeElement.GetString() == "object")
                            {
                                hasTypeObject = true;
                                break;
                            }
                        }
                    }
                }
                if (prop.Name == "properties")
                {
                    hasProperties = true;
                }
            }
            
            // If it's an object without properties, add empty properties
            if (hasTypeObject && !hasProperties)
            {
                _logger?.LogDebug("Transforming object without properties for Google compatibility");
                
                foreach (var prop in element.EnumerateObject())
                {
                    result[prop.Name] = TransformSchemaElement(prop.Value);
                }
                
                result["properties"] = new Dictionary<string, object>();
                
                return result;
            }
            
            // Otherwise, recursively transform all properties
            foreach (var prop in element.EnumerateObject())
            {
                // Skip fields that Google doesn't support
                if (prop.Name == "additionalProperties" || 
                    prop.Name == "$schema" || 
                    prop.Name == "$id" ||
                    prop.Name == "default" ||
                    prop.Name == "examples")
                {
                    _logger?.LogDebug("Skipping unsupported field '{Field}' for Google compatibility", prop.Name);
                    continue;
                }
                
                // Special handling for "type" property that's an array (union types)
                // Google doesn't support union types, so extract the first non-null type
                if (prop.Name == "type" && prop.Value.ValueKind == JsonValueKind.Array)
                {
                    var types = prop.Value.EnumerateArray()
                        .Where(t => t.ValueKind == JsonValueKind.String && t.GetString() != "null")
                        .ToList();
                    
                    if (types.Any())
                    {
                        result[prop.Name] = types.First().GetString()!;
                    }
                    else
                    {
                        result[prop.Name] = "string"; // Fallback
                    }
                }
                else
                {
                    result[prop.Name] = TransformSchemaElement(prop.Value);
                }
            }
            
            return result;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Select(TransformSchemaElement).ToList();
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString()!;
        }
        else if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out var intVal))
                return intVal;
            if (element.TryGetInt64(out var longVal))
                return longVal;
            return element.GetDouble();
        }
        else if (element.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        else if (element.ValueKind == JsonValueKind.False)
        {
            return false;
        }
        else if (element.ValueKind == JsonValueKind.Null)
        {
            return null!;
        }
        
        return element.ToString()!;
    }
    
    private LlmResponse ConvertToLlmResponse(GeminiResponse response)
    {
        string? text = null;
        var toolCalls = new List<LlmToolCall>();
        
        // Extract content from first candidate
        if (response.Candidates?.Any() == true)
        {
            var candidate = response.Candidates[0];
            
            if (candidate.Content?.Parts != null)
            {
                var textParts = new List<string>();
                
                foreach (var part in candidate.Content.Parts)
                {
                    if (part.Text != null)
                    {
                        textParts.Add(part.Text);
                    }
                    else if (part.FunctionCall != null)
                    {
                        // Google is requesting a function call
                        var callId = Guid.NewGuid().ToString(); // Google doesn't provide call IDs
                        var argsJson = JsonSerializer.Serialize(part.FunctionCall.Args ?? new Dictionary<string, object?>());
                        
                        toolCalls.Add(new LlmToolCall
                        {
                            Id = callId,
                            Name = part.FunctionCall.Name,
                            ArgumentsJson = argsJson
                        });
                    }
                }
                
                text = string.Concat(textParts);
            }
        }
        
        // Extract usage info
        LlmUsage? usage = null;
        if (response.UsageMetadata != null)
        {
            usage = new LlmUsage
            {
                InputTokens = response.UsageMetadata.PromptTokenCount,
                OutputTokens = response.UsageMetadata.CandidatesTokenCount
            };
        }
        
        return new LlmResponse
        {
            Text = text,
            ToolCalls = toolCalls.Any() ? toolCalls : null,
            FinishReason = null, // TODO: Map Google finish reasons
            Usage = usage
        };
    }
    
    // Response DTOs
    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
        
        [JsonPropertyName("usageMetadata")]
        public UsageMetadata? UsageMetadata { get; set; }
    }
    
    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
        
        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }
    
    private class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
        
        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }
    
    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
        
        [JsonPropertyName("functionCall")]
        public FunctionCall? FunctionCall { get; set; }
    }
    
    private class FunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("args")]
        public Dictionary<string, object?>? Args { get; set; }
    }
    
    private class UsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }
        
        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }
        
        [JsonPropertyName("totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }
}
