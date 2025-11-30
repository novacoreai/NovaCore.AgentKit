using System.Text.Json;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Generic base class for creating multimodal tools that can return images, audio, etc.
/// alongside text results. Used for scenarios like Computer Use where screenshots
/// need to be returned with each tool execution.
/// </summary>
/// <typeparam name="TArgs">The POCO type for tool arguments</typeparam>
public abstract class MultimodalTool<TArgs> : IMultimodalTool
{
    /// <summary>
    /// Unique name of the tool
    /// </summary>
    public abstract string Name { get; }
    
    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    public abstract string Description { get; }
    
    /// <summary>
    /// Execute the tool with strongly-typed arguments and return multimodal content
    /// </summary>
    /// <param name="args">Deserialized tool arguments</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tool execution result with optional additional content (e.g., screenshot)</returns>
    protected abstract Task<ToolResult> ExecuteAsync(TArgs args, CancellationToken ct);
    
    private JsonDocument? _cachedSchema;
    
    /// <summary>
    /// JSON Schema - automatically generated from TArgs type
    /// </summary>
    public JsonDocument ParameterSchema
    {
        get
        {
            _cachedSchema ??= SchemaGenerator.GenerateSchema<TArgs>();
            return _cachedSchema;
        }
    }
    
    /// <summary>
    /// Invoke the tool - handles JSON conversion automatically (backwards compatibility)
    /// </summary>
    public async Task<string> InvokeAsync(string argsJson, CancellationToken ct = default)
    {
        var result = await InvokeWithResultAsync(argsJson, ct);
        return result.Text;
    }
    
    /// <summary>
    /// Invoke the tool with multimodal result support
    /// </summary>
    public async Task<ToolResult> InvokeWithResultAsync(string argsJson, CancellationToken ct = default)
    {
        // Deserialize with case-insensitive matching
        var args = JsonHelper.DeserializeToolArgs<TArgs>(argsJson);
        
        if (args == null)
        {
            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = "Failed to deserialize tool arguments" 
                })
            };
        }
        
        try
        {
            // Execute with strongly-typed arguments
            return await ExecuteAsync(args, ct);
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = ex.Message 
                })
            };
        }
    }
}
