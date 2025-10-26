using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.Anthropic;

/// <summary>
/// Extension methods for adding Anthropic Claude provider to AgentBuilder
/// </summary>
public static class AnthropicAgentBuilderExtensions
{
    /// <summary>
    /// Use Anthropic Claude as the LLM provider
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="configure">Configuration callback</param>
    /// <returns>The agent builder for fluent chaining</returns>
    public static AgentBuilder UseAnthropic(
        this AgentBuilder builder,
        Action<AnthropicOptions> configure)
    {
        var options = new AnthropicOptions { ApiKey = "" }; // Will be set by configure
        configure(options);
        
        // Validate required fields
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for Anthropic provider", nameof(options));
        }
        
        // Create LLM client using direct REST implementation
        var llmClient = new AnthropicChatClient(options);
        
        // Register with builder (use internal method to pass model name for cost tracking)
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
    
    /// <summary>
    /// Use Anthropic Claude with direct API key and model
    /// </summary>
    public static AgentBuilder UseAnthropic(
        this AgentBuilder builder,
        string apiKey,
        string model = AnthropicModels.ClaudeSonnet45)
    {
        return builder.UseAnthropic(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });
    }
    
    /// <summary>
    /// Use Anthropic Claude with logger support
    /// </summary>
    public static AgentBuilder UseAnthropic(
        this AgentBuilder builder,
        Action<AnthropicOptions> configure,
        ILogger logger)
    {
        var options = new AnthropicOptions { ApiKey = "" };
        configure(options);
        
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for Anthropic provider", nameof(options));
        }
        
        var llmClient = new AnthropicChatClient(options, logger);
        
        builder.UseLlmClient(llmClient)
               .WithModel(options.Model);
        
        return builder;
    }
}
