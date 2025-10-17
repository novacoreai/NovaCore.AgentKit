namespace NovaCore.AgentKit.Core.RateLimiting;

/// <summary>
/// Rate limiter for controlling concurrent agent requests
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Try to acquire a slot for the given key
    /// </summary>
    Task<bool> TryAcquireAsync(string key, CancellationToken ct = default);
}

