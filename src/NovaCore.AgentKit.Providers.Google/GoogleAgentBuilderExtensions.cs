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
        
        // Get logger from builder
        var loggerFactory = builder.GetLoggerFactory();
        var logger = loggerFactory?.CreateLogger(typeof(GoogleChatClient).FullName!) ?? builder.GetLogger();
        
        // Create ILlmClient based on auth type
        ILlmClient chatClient;
        
        if (options.UseVertexAI)
        {
            chatClient = CreateVertexAIChatClient(options, logger);
        }
        else
        {
            chatClient = CreateGoogleAIChatClient(options, logger);
        }
        
        // Register with builder
        builder.UseLlmClient(chatClient);
        
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
    
    private static ILlmClient CreateGoogleAIChatClient(GoogleOptions options, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        // Create HttpClient for direct API calls (like Anthropic implementation)
        var httpClient = new HttpClient
        {
            Timeout = options.Timeout
        };
        
        // Use our custom GoogleChatClient that makes direct HTTP API calls
        // This gives us full control over tool schema handling
        // Google requires stricter schemas (no flexible objects/dictionaries)
        var chatClient = new GoogleChatClient(httpClient, options, logger);
        
        return chatClient;
    }
    
    private static ILlmClient CreateVertexAIChatClient(GoogleOptions options, Microsoft.Extensions.Logging.ILogger? logger = null)
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
