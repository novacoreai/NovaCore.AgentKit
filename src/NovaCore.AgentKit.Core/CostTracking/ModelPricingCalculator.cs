namespace NovaCore.AgentKit.Core.CostTracking;

/// <summary>
/// Calculator with predefined model pricing
/// </summary>
public class ModelPricingCalculator : ICostCalculator
{
    private readonly Dictionary<string, ModelPricing> _pricing = new()
    {
        // OpenAI Models
        ["gpt-4o"] = new ModelPricing
        {
            InputCostPer1M = 5.00m,
            OutputCostPer1M = 15.00m
        },
        ["gpt-4o-mini"] = new ModelPricing
        {
            InputCostPer1M = 0.15m,
            OutputCostPer1M = 0.60m
        },
        ["o1"] = new ModelPricing
        {
            InputCostPer1M = 15.00m,
            OutputCostPer1M = 60.00m
        },
        ["o1-mini"] = new ModelPricing
        {
            InputCostPer1M = 3.00m,
            OutputCostPer1M = 12.00m
        },
        ["o1-preview"] = new ModelPricing
        {
            InputCostPer1M = 15.00m,
            OutputCostPer1M = 60.00m
        },
        ["gpt-4-turbo"] = new ModelPricing
        {
            InputCostPer1M = 10.00m,
            OutputCostPer1M = 30.00m
        },
        ["gpt-4-turbo-preview"] = new ModelPricing
        {
            InputCostPer1M = 10.00m,
            OutputCostPer1M = 30.00m
        },
        ["gpt-4"] = new ModelPricing
        {
            InputCostPer1M = 30.00m,
            OutputCostPer1M = 60.00m
        },
        ["gpt-3.5-turbo"] = new ModelPricing
        {
            InputCostPer1M = 0.50m,
            OutputCostPer1M = 1.50m
        },
        ["gpt-5.1"] = new ModelPricing
        {
            InputCostPer1M = 1.25m,
            OutputCostPer1M = 10.00m
        },
        ["gpt-5.1-mini"] = new ModelPricing
        {
            InputCostPer1M = 0.25m,
            OutputCostPer1M = 2.00m
        },
        ["gpt-5.1-nano"] = new ModelPricing
        {
            InputCostPer1M = 0.05m,
            OutputCostPer1M = 0.40m
        },
        
        // Anthropic Models
        ["claude-sonnet-4-5-20250929"] = new ModelPricing
        {
            InputCostPer1M = 3.00m,
            OutputCostPer1M = 15.00m
        },
        ["claude-haiku-4-5-20251001"] = new ModelPricing
        {
            InputCostPer1M = 1.00m,
            OutputCostPer1M = 5.00m
        },
        ["claude-sonnet-4-20250514"] = new ModelPricing
        {
            InputCostPer1M = 3.00m,
            OutputCostPer1M = 15.00m
        },
        ["claude-3-7-sonnet-20250219"] = new ModelPricing
        {
            InputCostPer1M = 3.00m,
            OutputCostPer1M = 15.00m
        },
        
        // Google Gemini Models
        ["gemini-2.5-pro"] = new ModelPricing
        {
            InputCostPer1M = 1.25m,
            OutputCostPer1M = 10.00m
        },
        ["gemini-2.5-flash"] = new ModelPricing
        {
            InputCostPer1M = 0.30m,
            OutputCostPer1M = 2.50m
        },
        ["gemini-2.5-flash-lite"] = new ModelPricing
        {
            InputCostPer1M = 0.10m,
            OutputCostPer1M = 0.40m
        },
        ["gemini-flash-latest"] = new ModelPricing
        {
            InputCostPer1M = 0.10m,
            OutputCostPer1M = 0.40m
        },
        ["gemini-flash-lite-latest"] = new ModelPricing
        {
            InputCostPer1M = 0.075m,
            OutputCostPer1M = 0.30m
        },
        
        // XAI (Grok) Models
        ["grok-4-fast-non-reasoning"] = new ModelPricing
        {
            InputCostPer1M = 0.20m,
            OutputCostPer1M = 0.50m
        },
        ["grok-4-fast-reasoning"] = new ModelPricing
        {
            InputCostPer1M = 0.20m,
            OutputCostPer1M = 0.50m
        },
        ["grok-code-fast-1"] = new ModelPricing
        {
            InputCostPer1M = 0.20m,
            OutputCostPer1M = 1.50m
        },
        ["grok-4-1-fast-reasoning"] = new ModelPricing
        {
            InputCostPer1M = 0.20m,
            OutputCostPer1M = 0.50m
        },
        ["grok-4-1-fast-non-reasoning"] = new ModelPricing
        {
            InputCostPer1M = 0.20m,
            OutputCostPer1M = 0.50m
        },
        ["gemini-2.5-computer-use-preview-10-2025"] = new ModelPricing
        {
            InputCostPer1M = 1.25m,
            OutputCostPer1M = 10.00m
        },
        ["gemini-3-pro-preview"] = new ModelPricing
        {
            InputCostPer1M = 2.00m,
            OutputCostPer1M = 12.00m
        }
        
        // Note: Groq and OpenRouter pricing varies by model and tier
        // Models from these providers will return 0 cost (unknown)
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

