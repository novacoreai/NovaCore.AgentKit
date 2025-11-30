using FluentAssertions;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.CostTracking;
using NovaCore.AgentKit.Providers.OpenAI;
using Xunit;

namespace NovaCore.AgentKit.Tests.Core;

/// <summary>
/// Tests for cost tracking functionality
/// </summary>
public class CostTrackingTests
{
    [Fact]
    public void ModelPricingCalculator_CalculatesCorrectCosts()
    {
        var calculator = new ModelPricingCalculator();

        // Test GPT-4o pricing
        var cost = calculator.Calculate(OpenAIModels.GPT4o, 1000, 500);

        // Input: 1000 tokens * $5.00 per 1M = $0.005
        // Output: 500 tokens * $15.00 per 1M = $0.0075
        // Total: $0.0125
        cost.Should().BeApproximately(0.0125m, 0.0001m);
    }

    [Fact]
    public void ModelPricingCalculator_UnknownModel_ReturnsZero()
    {
        var calculator = new ModelPricingCalculator();

        var cost = calculator.Calculate("unknown-model", 1000, 500);

        cost.Should().Be(0m);
    }

    [Fact]
    public void ModelPricingCalculator_OnlyInputTokens()
    {
        var calculator = new ModelPricingCalculator();

        var cost = calculator.Calculate(OpenAIModels.GPT4o, 1000, 0);

        // Input: 1000 tokens * $5.00 per 1M = $0.005
        cost.Should().BeApproximately(0.005m, 0.0001m);
    }

    [Fact]
    public void ModelPricingCalculator_OnlyOutputTokens()
    {
        var calculator = new ModelPricingCalculator();

        var cost = calculator.Calculate(OpenAIModels.GPT4o, 0, 500);

        // Output: 500 tokens * $15.00 per 1M = $0.0075
        cost.Should().BeApproximately(0.0075m, 0.0001m);
    }

    [Fact]
    public void ModelPricingCalculator_LargeTokenCounts()
    {
        var calculator = new ModelPricingCalculator();

        // Test with 1M input tokens and 1M output tokens
        var cost = calculator.Calculate(OpenAIModels.GPT4o, 1_000_000, 1_000_000);

        // Input: 1M tokens * $5.00 per 1M = $5.00
        // Output: 1M tokens * $15.00 per 1M = $15.00
        // Total: $20.00
        cost.Should().BeApproximately(20.00m, 0.01m);
    }

    [Fact]
    public void LlmUsage_CostProperties_CalculateCorrectly()
    {
        var usage = new LlmUsage
        {
            InputTokens = 1000,
            OutputTokens = 500,
            InputCost = 0.005m,
            OutputCost = 0.0075m
        };

        usage.InputCost.Should().Be(0.005m);
        usage.OutputCost.Should().Be(0.0075m);
        usage.TotalCost.Should().Be(0.0125m);
        usage.TotalTokens.Should().Be(1500);
    }

    [Fact]
    public void LlmUsage_ZeroCost_PropertiesWork()
    {
        var usage = new LlmUsage
        {
            InputTokens = 1000,
            OutputTokens = 500,
            InputCost = 0m,
            OutputCost = 0m
        };

        usage.InputCost.Should().Be(0m);
        usage.OutputCost.Should().Be(0m);
        usage.TotalCost.Should().Be(0m);
        usage.TotalTokens.Should().Be(1500);
    }

    [Fact]
    public void ModelPricingCalculator_AllSupportedModels()
    {
        var calculator = new ModelPricingCalculator();

        // Test all major models have pricing
        var modelsWithPricing = new[]
        {
            OpenAIModels.GPT4o,
            OpenAIModels.GPT4oMini,
            OpenAIModels.O1,
            OpenAIModels.GPT4,
            OpenAIModels.GPT35Turbo
        };

        foreach (var model in modelsWithPricing)
        {
            var cost = calculator.Calculate(model, 100, 50);
            cost.Should().BeGreaterThan(0m, $"Model {model} should have pricing");
        }
    }

    [Fact]
    public void ModelPricingCalculator_AnthropicModels()
    {
        var calculator = new ModelPricingCalculator();

        var models = new[]
        {
            "claude-sonnet-4-5-20250929",
            "claude-haiku-4-5-20251001",
            "claude-sonnet-4-20250514",
            "claude-3-7-sonnet-20250219"
        };

        foreach (var model in models)
        {
            var cost = calculator.Calculate(model, 100, 50);
            cost.Should().BeGreaterThan(0m, $"Anthropic model {model} should have pricing");
        }
    }

    [Fact]
    public void ModelPricingCalculator_GoogleModels()
    {
        var calculator = new ModelPricingCalculator();

        var models = new[]
        {
            "gemini-2.5-pro",
            "gemini-2.5-flash",
            "gemini-2.5-flash-lite",
            "gemini-flash-latest"
        };

        foreach (var model in models)
        {
            var cost = calculator.Calculate(model, 100, 50);
            cost.Should().BeGreaterThan(0m, $"Google model {model} should have pricing");
        }
    }

    [Fact]
    public void ModelPricingCalculator_XAIModels()
    {
        var calculator = new ModelPricingCalculator();

        var models = new[]
        {
            "grok-4-fast-non-reasoning",
            "grok-4-fast-reasoning",
            "grok-code-fast-1"
        };

        foreach (var model in models)
        {
            var cost = calculator.Calculate(model, 100, 50);
            cost.Should().BeGreaterThan(0m, $"XAI model {model} should have pricing");
        }
    }

    [Fact]
    public void ModelPricingCalculator_SetCustomPricing()
    {
        var calculator = new ModelPricingCalculator();

        // Initially no pricing for custom model
        var initialCost = calculator.Calculate("custom-model", 100, 50);
        initialCost.Should().Be(0m);

        // Set custom pricing
        calculator.SetModelPricing("custom-model", 1.00m, 2.00m);

        // Now should have pricing
        var customCost = calculator.Calculate("custom-model", 100, 50);
        customCost.Should().BeApproximately(0.0003m, 0.0001m); // 100/1M * 1.00 + 50/1M * 2.00
    }
}
