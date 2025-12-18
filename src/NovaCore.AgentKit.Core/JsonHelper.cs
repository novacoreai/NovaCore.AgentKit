using System.Text.Json;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// JSON serialization helpers for tool argument handling
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Default JSON serializer options for tool arguments.
    /// Uses case-insensitive property matching for deserialization to handle variations in property names.
    /// Note: PropertyNamingPolicy only affects object serialization, not dictionary keys.
    /// </summary>
    public static readonly JsonSerializerOptions ToolArgumentOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    
    /// <summary>
    /// Helper for tools to deserialize arguments with case-insensitive matching
    /// </summary>
    /// <typeparam name="T">Type to deserialize to</typeparam>
    /// <param name="argsJson">JSON string from ITool.InvokeAsync</param>
    /// <returns>Deserialized object</returns>
    public static T? DeserializeToolArgs<T>(string argsJson)
    {
        return JsonSerializer.Deserialize<T>(argsJson, ToolArgumentOptions);
    }
    
    /// <summary>
    /// Format JSON for logging with proper unescaping for better readability.
    /// Removes unicode escapes like \u0022 for cleaner logs.
    /// </summary>
    public static string FormatForLogging(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;
            
        try
        {
            // Parse and re-serialize with relaxed encoding to avoid unicode escapes
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch
        {
            // If parsing fails, return as-is
            return json;
        }
    }
}

