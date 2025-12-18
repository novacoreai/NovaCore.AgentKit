namespace NovaCore.AgentKit.Core;

/// <summary>
/// Interface for LLM providers (replaces Microsoft.Extensions.AI.IChatClient)
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Get a single response from the LLM (non-streaming)
    /// </summary>
    Task<LlmResponse> GetResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a streaming response from the LLM
    /// </summary>
    IAsyncEnumerable<LlmStreamingUpdate> GetStreamingResponseAsync(
        List<LlmMessage> messages,
        LlmOptions? options = null,
        CancellationToken cancellationToken = default);
}

