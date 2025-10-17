namespace NovaCore.AgentKit.Core.CostTracking;

/// <summary>
/// Calculator with predefined model pricing
/// </summary>
public class ModelPricingCalculator : ICostCalculator
{
    private readonly Dictionary<string, ModelPricing> _pricing = new()
    {
        ["claude-sonnet-4-5-20250929"] = new ModelPricing
        {
            InputCostPer1M = 3.00m,
            OutputCostPer1M = 15.00m
        },
        ["grok-4-fast-non-reasoning"] = new ModelPricing
        {
            InputCostPer1M = 0.50m,
            OutputCostPer1M = 1.50m
        },
        ["gemini-2.5-flash"] = new ModelPricing
        {
            InputCostPer1M = 0.10m,
            OutputCostPer1M = 0.30m
        },
        ["gpt-4o"] = new ModelPricing
        {
            InputCostPer1M = 5.00m,
            OutputCostPer1M = 15.00m
        },
        ["gpt-4o-mini"] = new ModelPricing
        {
            InputCostPer1M = 0.15m,
            OutputCostPer1M = 0.60m
        }
    };
    
    public decimal Calculate(string model, int inputTokens, int outputTokens)
    {
        if (!_pricing.TryGetValue(model, out var pricing))
        {
            return 0; // Unknown model
        }
        
        var inputCost = (inputTokens / 1_000_000m) * pricing.InputCostPer1M;
        var outputCost = (outputTokens / 1_000_000m) * pricing.OutputCostPer1M;
        
        return inputCost + outputCost;
    }
    
    /// <summary>
    /// Add or update model pricing
    /// </summary>
    public void SetModelPricing(string model, decimal inputCostPer1M, decimal outputCostPer1M)
    {
        _pricing[model] = new ModelPricing
        {
            InputCostPer1M = inputCostPer1M,
            OutputCostPer1M = outputCostPer1M
        };
    }
}

/// <summary>
/// Pricing for a model
/// </summary>
public class ModelPricing
{
    public decimal InputCostPer1M { get; init; }
    public decimal OutputCostPer1M { get; init; }
}

