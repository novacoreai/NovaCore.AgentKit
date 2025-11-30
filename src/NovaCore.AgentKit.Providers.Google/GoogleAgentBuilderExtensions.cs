using Microsoft.Extensions.AI;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Providers.Google;

/// <summary>
/// Extension methods for adding Google Gemini provider to AgentBuilder
/// </summary>
public static class GoogleAgentBuilderExtensions
{
    /// <summary>
    /// Use Google Gemini as the LLM provider
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="configure">Configuration callback</param>
    /// <returns>The agent builder for fluent chaining</returns>
    public static AgentBuilder UseGoogle(
        this AgentBuilder builder,
        Action<GoogleOptions> configure)
    {
        var options = new GoogleOptions();
        configure(options);
        
        // Validate configuration
        if (options.UseVertexAI)
        {
            if (string.IsNullOrEmpty(options.ProjectId) || string.IsNullOrEmpty(options.Location))
            {
                throw new ArgumentException("ProjectId and Location are required for Vertex AI");
            }
            
            if (string.IsNullOrEmpty(options.CredentialsJson))
            {
                throw new ArgumentException("CredentialsJson is required for Vertex AI. Host app should read from file/vault and provide as string.");
            }
        }
        else
        {
            if (string.IsNullOrEmpty(options.ApiKey))
            {
                throw new ArgumentException("ApiKey is required for Google AI");
            }
        }
        
        // Create ILlmClient based on auth type
        ILlmClient chatClient;
        
        if (options.UseVertexAI)
        {
            chatClient = CreateVertexAIChatClient(options);
        }
        else
        {
            chatClient = CreateGoogleAIChatClient(options);
        }
        
        // Register with builder (use internal method to pass model name for cost tracking)
        builder.UseLlmClient(chatClient)
               .WithModel(options.Model);
        
        return builder;
    }
    
    /// <summary>
    /// Use Google Gemini with direct API key and model
    /// </summary>
    public static AgentBuilder UseGoogle(
        this AgentBuilder builder,
        string apiKey,
        string model = GoogleModels.Gemini25Flash)
    {
        return builder.UseGoogle(options =>
        {
            options.ApiKey = apiKey;
            options.Model = model;
        });
    }
    
    /// <summary>
    /// Use Google Gemini Computer Use model with browser automation capabilities
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="apiKey">Google AI API key</param>
    /// <param name="excludedFunctions">Optional list of Computer Use functions to exclude (e.g., "drag_and_drop")</param>
    /// <returns>The agent builder for fluent chaining</returns>
    public static AgentBuilder UseGoogleComputerUse(
        this AgentBuilder builder,
        string apiKey,
        List<string>? excludedFunctions = null)
    {
        return builder.UseGoogle(options =>
        {
            options.ApiKey = apiKey;
            options.Model = GoogleModels.Gemini25ComputerUsePreview;
            options.EnableComputerUse = true;
            options.ComputerUseEnvironment = ComputerUseEnvironment.Browser;
            options.ExcludedComputerUseFunctions = excludedFunctions;
        });
    }
    
    private static ILlmClient CreateGoogleAIChatClient(GoogleOptions options)
    {
        // Create HttpClient for direct API calls (like Anthropic implementation)
        var httpClient = new HttpClient
        {
            Timeout = options.Timeout
        };
        
        // Use our custom GoogleChatClient that makes direct HTTP API calls
        // This gives us full control over tool schema handling
        // Google requires stricter schemas (no flexible objects/dictionaries)
        var chatClient = new GoogleChatClient(httpClient, options);
        
        return chatClient;
    }
    
    private static ILlmClient CreateVertexAIChatClient(GoogleOptions options)
    {
        // For Vertex AI support with GCP credentials
        // The host app should:
        // 1. Read the service account JSON from file/vault
        // 2. Either set GOOGLE_APPLICATION_CREDENTIALS environment variable
        // 3. Or pass the JSON content to us
        
        // Since GenerativeAIChatClient constructor may not support Vertex AI directly,
        // we'll need to use the underlying Google_GenerativeAI SDK
        
        // For now, create using API key pattern
        // TODO: Implement proper Vertex AI support by using VertexAI class directly
        // and wrapping in IChatClient adapter
        
        throw new NotImplementedException(
            "Vertex AI support requires direct SDK integration. " +
            "Please use Google AI with API key for now, or help implement Vertex AI wrapper.");
    }
}
