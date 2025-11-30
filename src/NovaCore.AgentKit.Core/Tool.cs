using System.Text.Json;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Generic base class for creating strongly-typed tools.
/// Automatically handles JSON schema generation and serialization/deserialization.
/// </summary>
/// <typeparam name="TArgs">The POCO type for tool arguments</typeparam>
/// <typeparam name="TResult">The POCO type for tool results</typeparam>
public abstract class Tool<TArgs, TResult> : ITool
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
    /// Execute the tool with strongly-typed arguments
    /// </summary>
    /// <param name="args">Deserialized tool arguments</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    protected abstract Task<TResult> ExecuteAsync(TArgs args, CancellationToken ct);
    
    // ITool implementation - auto-generated from generic types
    
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
    /// Invoke the tool - handles JSON conversion automatically
    /// </summary>
    public async Task<string> InvokeAsync(string argsJson, CancellationToken ct = default)
    {
        // Deserialize with case-insensitive matching
        var args = JsonHelper.DeserializeToolArgs<TArgs>(argsJson);
        
        if (args == null)
        {
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = "Failed to deserialize tool arguments" 
            });
        }
        
        try
        {
            // Execute with strongly-typed arguments
            var result = await ExecuteAsync(args, ct);
            
            // Serialize result
            return JsonSerializer.Serialize(result, JsonHelper.ToolArgumentOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = ex.Message 
            });
        }
    }
}

/// <summary>
/// Simplified tool base class for tools that don't return complex results.
/// Returns a simple success/error response.
/// </summary>
/// <typeparam name="TArgs">The POCO type for tool arguments</typeparam>
public abstract class SimpleTool<TArgs> : Tool<TArgs, ToolResponse>
{
    /// <summary>
    /// Execute the tool with strongly-typed arguments.
    /// Return a message string - success/error handling is automatic.
    /// </summary>
    /// <param name="args">Deserialized tool arguments</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message or result description</returns>
    protected abstract Task<string> RunAsync(TArgs args, CancellationToken ct);
    
    protected override async Task<ToolResponse> ExecuteAsync(TArgs args, CancellationToken ct)
    {
        try
        {
            var message = await RunAsync(args, ct);
            return new ToolResponse { Success = true, Message = message };
        }
        catch (Exception ex)
        {
            return new ToolResponse { Success = false, Error = ex.Message };
        }
    }
}

/// <summary>
/// Standard tool response for simple tools
/// </summary>
public class ToolResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Base class for UI tools that support POCO arguments and results.
/// UI tools are intercepted by the agent and returned to the host for user interaction.
/// The host application displays appropriate UI and sends the result back via SendAsync.
/// </summary>
/// <typeparam name="TArgs">The POCO type for tool arguments</typeparam>
/// <typeparam name="TResult">The POCO type for tool results</typeparam>
public abstract class UITool<TArgs, TResult> : Tool<TArgs, TResult>, IUITool
{
    protected override Task<TResult> ExecuteAsync(TArgs args, CancellationToken ct)
    {
        // UI tools are never executed internally - they're intercepted by the agent
        throw new InvalidOperationException(
            $"UI tool '{Name}' should not be executed internally. " +
            "It should be intercepted by the agent and handled by the host application.");
    }
}

