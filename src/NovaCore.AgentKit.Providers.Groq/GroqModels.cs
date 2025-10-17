namespace NovaCore.AgentKit.Providers.Groq;

/// <summary>
/// Well-known Groq model identifiers
/// </summary>
public static class GroqModels
{
    // Meta Llama 3.3 models (RECOMMENDED for tool calling)
    /// <summary>Llama 3.3 70B Versatile - 128K context, excellent tool calling support</summary>
    public const string Llama3_3_70B = "llama-3.3-70b-versatile";
    
    // Meta Llama 3.1 models (RECOMMENDED for tool calling)
    /// <summary>Llama 3.1 70B Versatile - 128K context, reliable tool calling</summary>
    public const string Llama3_1_70B = "llama-3.1-70b-versatile";
    
    /// <summary>Llama 3.1 8B Instant - Fast, smaller model with tool calling support</summary>
    public const string Llama3_1_8B = "llama-3.1-8b-instant";
    
    // Meta Llama 4 models
    /// <summary>Llama 4 Maverick 17B 128E Instruct - 128K context window, 8K max completion</summary>
    public const string Llama4Maverick17B = "meta-llama/llama-4-maverick-17b-128e-instruct";
    
    // Qwen models
    /// <summary>Qwen 3 32B - 128K context window, 40K max completion</summary>
    public const string Qwen3_32B = "qwen/qwen3-32b";
    
    // OpenAI OSS models (WARNING: Known issues with tool calling)
    /// <summary>GPT OSS 120B - 128K context window, 65K max completion (UNRELIABLE tool calling)</summary>
    public const string GptOss120B = "openai/gpt-oss-120b";
    
    /// <summary>GPT OSS 20B - 128K context window, 65K max completion (UNRELIABLE tool calling)</summary>
    public const string GptOss20B = "openai/gpt-oss-20b";
    
    // Moonshot AI models
    /// <summary>Kimi K2 Instruct - 256K context window, 16K max completion</summary>
    public const string KimiK2Instruct = "moonshotai/kimi-k2-instruct-0905";
}

