using System.Collections.Concurrent;

namespace NovaCore.AgentKit.Core.RateLimiting;

/// <summary>
/// Semaphore-based rate limiter
/// </summary>
public class RateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
    private readonly RateLimitConfig _config;
    
    public RateLimiter(RateLimitConfig config)
    {
        _config = config;
    }
    
    public async Task<bool> TryAcquireAsync(string key, CancellationToken ct)
    {
        var semaphore = _semaphores.GetOrAdd(
            key,
            _ => new SemaphoreSlim(_config.MaxConcurrent, _config.MaxConcurrent));
        
        return await semaphore.WaitAsync(_config.Timeout, ct);
    }
}

/// <summary>
/// Configuration for rate limiting
/// </summary>
public class RateLimitConfig
{
    /// <summary>Maximum concurrent requests per key</summary>
    public int MaxConcurrent { get; set; } = 5;
    
    /// <summary>Timeout for acquiring a slot</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Exception thrown when rate limit is exceeded
/// </summary>
public class RateLimitExceededException : Exception
{
    public RateLimitExceededException(string message) : base(message)
    {
    }
}

