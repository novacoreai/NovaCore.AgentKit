using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.XAI;

/// <summary>
/// Extension methods for adding XAI provider to AgentBuilder
/// </summary>
public static class XAIAgentBuilderExtensions
{
    /// <summary>
    /// Use XAI (Grok) as the LLM provider (via OpenAI-compatible API)
    /// </summary>
    public static AgentBuilder UseXAI(
        this AgentBuilder builder,
        Action<XAIOptions> configure)
    {
        var options = new XAIOptions { ApiKey = "" };
        configure(options);
        
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for XAI provider", nameof(options));
        }
        
        // Create custom LLM client
        var llmClient = new XAILlmClient(options);
        
        // Register with builder (use internal method to pass model name for cost tracking)
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
    
    /// <summary>
    /// Use XAI with direct API key and model
    /// </summary>
    public static AgentBuilder UseXAI(
        this AgentBuilder builder,
        string apiKey,
        string model = XAIModels.Grok4FastNonReasoning)
    {
        return builder.UseXAI(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });
    }
    
    /// <summary>
    /// Use XAI with logger support
    /// </summary>
    internal static AgentBuilder UseXAI(
        this AgentBuilder builder,
        Action<XAIOptions> configure,
        ILogger? logger)
    {
        var options = new XAIOptions { ApiKey = "" };
        configure(options);
        
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for XAI provider", nameof(options));
        }
        
        var llmClient = new XAILlmClient(options, logger);
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
}
