using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaCore.AgentKit.Providers.Anthropic.Models;

/// <summary>
/// Base class for all streaming events from Anthropic API
/// </summary>
[JsonConverter(typeof(StreamingEventConverter))]
public abstract class AnthropicStreamingEvent
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
}

/// <summary>
/// Message start event
/// </summary>
public class MessageStartEvent : AnthropicStreamingEvent
{
    [JsonPropertyName("message")]
    public required AnthropicResponse Message { get; set; }
}

/// <summary>
/// Content block start event
/// </summary>
public class ContentBlockStartEvent : AnthropicStreamingEvent
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("content_block")]
    public object? ContentBlock { get; set; }
}

/// <summary>
/// Content block delta event
/// </summary>
public class ContentBlockDeltaEvent : AnthropicStreamingEvent
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("delta")]
    public required object Delta { get; set; }
}

/// <summary>
/// Content block stop event
/// </summary>
public class ContentBlockStopEvent : AnthropicStreamingEvent
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
}

/// <summary>
/// Message delta event
/// </summary>
public class MessageDeltaEvent : AnthropicStreamingEvent
{
    [JsonPropertyName("delta")]
    public required MessageDelta Delta { get; set; }
    
    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
}

/// <summary>
/// Message stop event
/// </summary>
public class MessageStopEvent : AnthropicStreamingEvent
{
}

/// <summary>
/// Ping event (keepalive)
/// </summary>
public class PingEvent : AnthropicStreamingEvent
{
}

/// <summary>
/// Error event
/// </summary>
public class ErrorEvent : AnthropicStreamingEvent
{
    [JsonPropertyName("error")]
    public required AnthropicError Error { get; set; }
}

/// <summary>
/// Message delta information
/// </summary>
public class MessageDelta
{
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
    
    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }
}

/// <summary>
/// Text delta
/// </summary>
public class TextDelta
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text_delta";
    
    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

/// <summary>
/// Input JSON delta (for tool calls)
/// </summary>
public class InputJsonDelta
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "input_json_delta";
    
    [JsonPropertyName("partial_json")]
    public required string PartialJson { get; set; }
}

/// <summary>
/// Custom JSON converter for streaming events
/// </summary>
public class StreamingEventConverter : JsonConverter<AnthropicStreamingEvent>
{
    public override AnthropicStreamingEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("type", out var typeElement))
        {
            throw new JsonException("Missing 'type' property in streaming event");
        }
        
        var eventType = typeElement.GetString();
        
        AnthropicStreamingEvent? result = eventType switch
        {
            "message_start" => JsonSerializer.Deserialize<MessageStartEvent>(root.GetRawText(), options),
            "content_block_start" => JsonSerializer.Deserialize<ContentBlockStartEvent>(root.GetRawText(), options),
            "content_block_delta" => DeserializeContentBlockDelta(root, options),
            "content_block_stop" => JsonSerializer.Deserialize<ContentBlockStopEvent>(root.GetRawText(), options),
            "message_delta" => JsonSerializer.Deserialize<MessageDeltaEvent>(root.GetRawText(), options),
            "message_stop" => JsonSerializer.Deserialize<MessageStopEvent>(root.GetRawText(), options),
            "ping" => JsonSerializer.Deserialize<PingEvent>(root.GetRawText(), options),
            "error" => JsonSerializer.Deserialize<ErrorEvent>(root.GetRawText(), options),
            _ => throw new JsonException($"Unknown streaming event type: {eventType}")
        };
        
        return result ?? throw new JsonException($"Failed to deserialize event of type: {eventType}");
    }
    
    private static ContentBlockDeltaEvent? DeserializeContentBlockDelta(JsonElement root, JsonSerializerOptions options)
    {
        var deltaEvent = JsonSerializer.Deserialize<ContentBlockDeltaEvent>(root.GetRawText(), options);
        
        if (deltaEvent == null)
        {
            return null;
        }
        
        // Parse the delta object based on its type
        if (deltaEvent.Delta is JsonElement deltaElement)
        {
            if (deltaElement.TryGetProperty("type", out var deltaType))
            {
                object? parsedDelta = deltaType.GetString() switch
                {
                    "text_delta" => JsonSerializer.Deserialize<TextDelta>(deltaElement.GetRawText(), options),
                    "input_json_delta" => JsonSerializer.Deserialize<InputJsonDelta>(deltaElement.GetRawText(), options),
                    _ => (object?)deltaElement
                };
                
                if (parsedDelta != null)
                {
                    deltaEvent.Delta = parsedDelta;
                }
            }
        }
        
        return deltaEvent;
    }
    
    public override void Write(Utf8JsonWriter writer, AnthropicStreamingEvent value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

