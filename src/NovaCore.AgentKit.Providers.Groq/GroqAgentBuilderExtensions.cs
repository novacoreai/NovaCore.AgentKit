using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.Groq;

/// <summary>
/// Extension methods for adding Groq provider to AgentBuilder
/// </summary>
public static class GroqAgentBuilderExtensions
{
    /// <summary>
    /// Use Groq as the LLM provider (via OpenAI-compatible API)
    /// </summary>
    public static AgentBuilder UseGroq(
        this AgentBuilder builder,
        Action<GroqOptions> configure)
    {
        var options = new GroqOptions { ApiKey = "" };
        configure(options);
        
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for Groq provider", nameof(options));
        }
        
        // Create custom LLM client
        var llmClient = new GroqLlmClient(options);
        
        // Register with builder (use internal method to pass model name for cost tracking)
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
    
    /// <summary>
    /// Use Groq with direct API key and model
    /// </summary>
    public static AgentBuilder UseGroq(
        this AgentBuilder builder,
        string apiKey,
        string model = GroqModels.Llama3_3_70B)
    {
        return builder.UseGroq(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });
    }
    
    /// <summary>
    /// Use Groq with logger support
    /// </summary>
    internal static AgentBuilder UseGroq(
        this AgentBuilder builder,
        Action<GroqOptions> configure,
        ILogger? logger)
    {
        var options = new GroqOptions { ApiKey = "" };
        configure(options);
        
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for Groq provider", nameof(options));
        }
        
        var llmClient = new GroqLlmClient(options, logger);
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
}
