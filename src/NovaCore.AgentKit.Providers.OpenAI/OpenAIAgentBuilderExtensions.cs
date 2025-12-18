using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.OpenAI;

/// <summary>
/// Extension methods for adding OpenAI provider to AgentBuilder
/// </summary>
public static class OpenAIAgentBuilderExtensions
{
    /// <summary>
    /// Use OpenAI as the LLM provider
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="configure">Configuration callback</param>
    /// <returns>The agent builder for fluent chaining</returns>
    public static AgentBuilder UseOpenAI(
        this AgentBuilder builder,
        Action<OpenAIOptions> configure)
    {
        var options = new OpenAIOptions { ApiKey = "" }; // Will be set by configure
        configure(options);
        
        // Validate required fields
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for OpenAI provider", nameof(options));
        }
        
        // Create custom LLM client
        var llmClient = new OpenAILlmClient(options);
        
        // Register with builder (use internal method to pass model name for cost tracking)
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
    
    /// <summary>
    /// Use OpenAI with direct API key and model
    /// </summary>
    public static AgentBuilder UseOpenAI(
        this AgentBuilder builder,
        string apiKey,
        string model = OpenAIModels.GPT4o)
    {
        return builder.UseOpenAI(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });
    }
    
    /// <summary>
    /// Use OpenAI with logger support
    /// </summary>
    internal static AgentBuilder UseOpenAI(
        this AgentBuilder builder,
        Action<OpenAIOptions> configure,
        ILogger? logger)
    {
        var options = new OpenAIOptions { ApiKey = "" };
        configure(options);
        
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for OpenAI provider", nameof(options));
        }
        
        var llmClient = new OpenAILlmClient(options, logger);
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
}

