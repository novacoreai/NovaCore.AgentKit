using System.Text.Json;
using NJsonSchema;
using NJsonSchema.Generation;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Generates JSON schemas from C# types for tool parameters
/// </summary>
public static class SchemaGenerator
{
    private static readonly SystemTextJsonSchemaGeneratorSettings Settings = new()
    {
        DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
        SerializerOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        }
    };
    
    /// <summary>
    /// Generate JSON schema from a C# type
    /// </summary>
    /// <typeparam name="T">The type to generate schema for</typeparam>
    /// <returns>JsonDocument representing the JSON schema</returns>
    public static JsonDocument GenerateSchema<T>()
    {
        var schema = JsonSchema.FromType<T>(Settings);
        var schemaJson = schema.ToJson();
        return JsonDocument.Parse(schemaJson);
    }
    
    /// <summary>
    /// Generate JSON schema from a C# type
    /// </summary>
    /// <param name="type">The type to generate schema for</param>
    /// <returns>JsonDocument representing the JSON schema</returns>
    public static JsonDocument GenerateSchema(Type type)
    {
        var schema = JsonSchema.FromType(type, Settings);
        var schemaJson = schema.ToJson();
        return JsonDocument.Parse(schemaJson);
    }
}


