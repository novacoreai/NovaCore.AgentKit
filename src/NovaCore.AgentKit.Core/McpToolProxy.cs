using System.Text.Json;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Proxy that adapts MCP tool definitions to ITool interface
/// </summary>
internal class McpToolProxy : ITool
{
    private readonly IMcpClient _mcpClient;
    private readonly McpToolDefinition _definition;
    
    public McpToolProxy(IMcpClient mcpClient, McpToolDefinition definition)
    {
        _mcpClient = mcpClient;
        _definition = definition;
    }
    
    public string Name => _definition.Name;
    
    public string Description => _definition.Description;
    
    public JsonDocument ParameterSchema => _definition.InputSchema;
    
    public async Task<string> InvokeAsync(string argsJson, CancellationToken ct = default)
    {
        // Parse arguments
        // Handle empty or whitespace JSON
        JsonElement parsedArgs;
        if (string.IsNullOrWhiteSpace(argsJson))
        {
            parsedArgs = JsonDocument.Parse("{}").RootElement;
        }
        else
        {
            parsedArgs = JsonSerializer.Deserialize<JsonElement>(argsJson);
        }
        
        Dictionary<string, object?> args;
        
        // Handle OpenAI/XAI-style argument wrapping
        // Some models wrap arguments in "args" or "parameters" keys
        if (parsedArgs.ValueKind == JsonValueKind.Object)
        {
            // Check if arguments are wrapped in "args" key (OpenAI/XAI pattern)
            if (parsedArgs.TryGetProperty("args", out var argsProperty) && 
                parsedArgs.GetProperty("args").ValueKind == JsonValueKind.Object)
            {
                // Only unwrap if "args" is the sole property
                var propertyCount = 0;
                foreach (var _ in parsedArgs.EnumerateObject()) propertyCount++;
                
                if (propertyCount == 1)
                {
                    args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsProperty.GetRawText())
                        ?? new Dictionary<string, object?>();
                }
                else
                {
                    args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson)
                        ?? new Dictionary<string, object?>();
                }
            }
            // Check if arguments are wrapped in "parameters" key
            else if (parsedArgs.TryGetProperty("parameters", out var paramsProperty) &&
                     parsedArgs.GetProperty("parameters").ValueKind == JsonValueKind.Object)
            {
                var propertyCount = 0;
                foreach (var _ in parsedArgs.EnumerateObject()) propertyCount++;
                
                if (propertyCount == 1)
                {
                    args = JsonSerializer.Deserialize<Dictionary<string, object?>>(paramsProperty.GetRawText())
                        ?? new Dictionary<string, object?>();
                }
                else
                {
                    args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson)
                        ?? new Dictionary<string, object?>();
                }
            }
            else
            {
                args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson)
                    ?? new Dictionary<string, object?>();
            }
        }
        else
        {
            args = new Dictionary<string, object?>();
        }
        
        var result = await _mcpClient.CallToolAsync(Name, args, ct);
        
        if (!result.Success)
        {
            return JsonSerializer.Serialize(new { error = result.Error, success = false });
        }
        
        // Extract content from the MCP result
        var resultStr = result.Data?.GetRawText() ?? "{}";
        
        if (string.IsNullOrWhiteSpace(resultStr) || resultStr == "{}" || resultStr == "null")
        {
            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"Tool '{Name}' executed successfully with no output" 
            });
        }
        
        return resultStr;
    }
}

