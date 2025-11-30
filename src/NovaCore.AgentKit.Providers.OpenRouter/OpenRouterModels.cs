namespace NovaCore.AgentKit.Providers.OpenRouter;

/// <summary>
/// Well-known OpenRouter model identifiers (prefix with provider name)
/// </summary>
public static class OpenRouterModels
{
    // Anthropic models via OpenRouter
    /// <summary>Claude 3.5 Sonnet via OpenRouter</summary>
    public const string Claude35Sonnet = "anthropic/claude-3.5-sonnet";
    
    /// <summary>Claude 3.5 Haiku via OpenRouter</summary>
    public const string Claude35Haiku = "anthropic/claude-3.5-haiku";
    
    // OpenAI models via OpenRouter
    /// <summary>GPT-4o via OpenRouter</summary>
    public const string GPT4o = "openai/gpt-4o";
    
    /// <summary>GPT-4o Mini via OpenRouter</summary>
    public const string GPT4oMini = "openai/gpt-4o-mini";
    
    /// <summary>o1 via OpenRouter</summary>
    public const string O1 = "openai/o1";
    
    // Google models via OpenRouter
    /// <summary>Gemini 2.0 Flash via OpenRouter</summary>
    public const string Gemini20Flash = "google/gemini-2.0-flash";
    
    /// <summary>Gemini 1.5 Pro via OpenRouter</summary>
    public const string Gemini15Pro = "google/gemini-1.5-pro";
    
    // Meta Llama models via OpenRouter
    /// <summary>Llama 3.3 70B via OpenRouter</summary>
    public const string Llama33_70B = "meta-llama/llama-3.3-70b-instruct";
    
    /// <summary>Llama 3.1 405B via OpenRouter</summary>
    public const string Llama31_405B = "meta-llama/llama-3.1-405b-instruct";
    
    // xAI models via OpenRouter
    /// <summary>Grok 2 via OpenRouter</summary>
    public const string Grok2 = "x-ai/grok-2";
    
    // DeepSeek models via OpenRouter
    /// <summary>DeepSeek Chat via OpenRouter</summary>
    public const string DeepSeekChat = "deepseek/deepseek-chat";
    
    // Perplexity models via OpenRouter
    /// <summary>Perplexity Sonar via OpenRouter</summary>
    public const string PerplexitySonar = "perplexity/sonar-pro";
}

