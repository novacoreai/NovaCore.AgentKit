namespace NovaCore.AgentKit.Providers.Google;

/// <summary>
/// Well-known Google Gemini model identifiers
/// </summary>
public static class GoogleModels
{
    // Gemini 2.5 models
    /// <summary>Gemini 2.5 Pro - Most capable Gemini 2.5 model</summary>
    public const string Gemini25Pro = "gemini-2.5-pro";
    
    /// <summary>Gemini 2.5 Flash - Fast and efficient</summary>
    public const string Gemini25Flash = "gemini-2.5-flash";
    
    /// <summary>Gemini 2.5 Flash Lite - Lightweight and fast</summary>
    public const string Gemini25FlashLite = "gemini-2.5-flash-lite";
    
    // Latest models (automatically updated)
    /// <summary>Gemini Flash Latest - Always points to the latest flash model</summary>
    public const string GeminiFlashLatest = "gemini-flash-latest";
    
    /// <summary>Gemini Flash Lite Latest - Always points to the latest flash lite model</summary>
    public const string GeminiFlashLiteLatest = "gemini-flash-lite-latest";
}

