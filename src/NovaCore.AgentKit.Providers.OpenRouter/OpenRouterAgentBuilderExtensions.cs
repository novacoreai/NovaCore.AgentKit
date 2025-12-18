using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.OpenRouter;

/// <summary>
/// Extension methods for adding OpenRouter provider to AgentBuilder
/// </summary>
public static class OpenRouterAgentBuilderExtensions
{
    /// <summary>
    /// Use OpenRouter as the LLM provider (via OpenAI-compatible API)
    /// </summary>
    public static AgentBuilder UseOpenRouter(
        this AgentBuilder builder,
        Action<OpenRouterOptions> configure)
    {
        var options = new OpenRouterOptions { ApiKey = "" };
        configure(options);
        
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for OpenRouter provider", nameof(options));
        }
        
        // Create custom LLM client
        var llmClient = new OpenRouterLlmClient(options);
        
        // Register with builder (use internal method to pass model name for cost tracking)
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
    
    /// <summary>
    /// Use OpenRouter with direct API key and model
    /// </summary>
    public static AgentBuilder UseOpenRouter(
        this AgentBuilder builder,
        string apiKey,
        string model)
    {
        return builder.UseOpenRouter(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });
    }
    
    /// <summary>
    /// Use OpenRouter with logger support
    /// </summary>
    internal static AgentBuilder UseOpenRouter(
        this AgentBuilder builder,
        Action<OpenRouterOptions> configure,
        ILogger? logger)
    {
        var options = new OpenRouterOptions { ApiKey = "" };
        configure(options);
        
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for OpenRouter provider", nameof(options));
        }
        
        var llmClient = new OpenRouterLlmClient(options, logger);
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
}
