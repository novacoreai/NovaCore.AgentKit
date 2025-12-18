namespace NovaCore.AgentKit.Core.CostTracking;

/// <summary>
/// Tracks token usage and costs
/// </summary>
public interface ICostTracker
{
    /// <summary>
    /// Track token usage for a model
    /// </summary>
    void TrackTokenUsage(string model, int inputTokens, int outputTokens);
    
    /// <summary>
    /// Get cost summary
    /// </summary>
    CostSummary GetSummary();
    
    /// <summary>
    /// Reset all tracked costs
    /// </summary>
    void Reset();
}

