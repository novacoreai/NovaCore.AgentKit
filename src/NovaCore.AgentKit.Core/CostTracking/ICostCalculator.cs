namespace NovaCore.AgentKit.Core.CostTracking;

/// <summary>
/// Calculates costs based on token usage
/// </summary>
public interface ICostCalculator
{
    /// <summary>
    /// Calculate cost for token usage
    /// </summary>
    decimal Calculate(string model, int inputTokens, int outputTokens);
}

