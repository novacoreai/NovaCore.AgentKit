# NovaCore.AgentKit Cost Tracking Implementation

## Overview
Added comprehensive cost and token usage tracking to the agent library, enabling real-time monitoring of LLM API costs and usage patterns.

## Changes Made

### Core Classes Updated

#### `LlmUsage` (LlmResponse.cs)
- **Added cost fields:**
  - `InputCost`: Cost for input tokens (USD)
  - `OutputCost`: Cost for output tokens (USD)
  - `TotalCost`: Computed total cost (InputCost + OutputCost)

#### `LlmResponseEvent` (IAgentObserver.cs)
- **Added `ModelName` field:** Identifies which model was used for the LLM call
- **Updated to include cost data:** LlmUsage now contains cost information

#### `AgentTurn` (AgentTurn.cs)
- **Added cumulative tracking:**
  - `TotalInputTokens`: Total input tokens used in the turn
  - `TotalOutputTokens`: Total output tokens used in the turn
  - `TotalCost`: Total cost for the entire turn (USD)

#### `Agent` (Agent.cs)
- **Enhanced constructor:** Added `modelName` and `costCalculator` parameters
- **Real-time cost calculation:** Calculates costs for each LLM response
- **Turn-level accumulation:** Tracks cumulative tokens and costs throughout the turn
- **Model-aware:** Passes model name to events for accurate cost tracking
- **Error handling:** Returns 0 cost for unknown models (as requested)

#### `AgentBuilder` (AgentBuilder.cs)
- **Added cost calculator support:** Default `ModelPricingCalculator` instance
- **Model name tracking:** Stores and passes model name to Agent
- **Fluent API:** Added `WithCostCalculator()` method for custom cost calculators
- **Internal method:** Added `UseLlmClient(llmClient, modelName)` for provider integration

### Provider Extensions Updated

All 6 provider extensions updated to pass model names:
- **OpenAI:** `OpenAIAgentBuilderExtensions.cs`
- **Anthropic:** `AnthropicAgentBuilderExtensions.cs`
- **Google:** `GoogleAgentBuilderExtensions.cs`
- **Groq:** `GroqAgentBuilderExtensions.cs`
- **XAI:** `XAIAgentBuilderExtensions.cs`
- **OpenRouter:** `OpenRouterAgentBuilderExtensions.cs`

### Cost Calculator Enhanced

#### `ModelPricingCalculator` (ModelPricingCalculator.cs)
- **Comprehensive pricing database:** Added pricing for 25+ models
- **Organized by provider:**
  - **OpenAI:** 9 models (GPT-4o, GPT-4o-mini, o1 series, GPT-4, GPT-3.5)
  - **Anthropic:** 4 models (Claude 4.5, 4, 3.7 series)
  - **Google:** 5 models (Gemini 2.5 Pro, Flash, Flash Lite)
  - **XAI:** 3 models (Grok 4 variants)
- **Unknown model handling:** Returns $0.00 for unrecognized models
- **Accurate pricing:** Based on current API rates (as of October 2025)

### Cost Tracking Flow

#### Per-LLM-Response (Granular)
```csharp
public void OnLlmResponse(LlmResponseEvent evt)
{
    // Model: gpt-4o
    // Tokens: 150 input, 75 output
    // Cost: $0.001875 (input: $0.00075, output: $0.001125)
}
```

#### Per-Turn (Summary)
```csharp
public void OnTurnComplete(TurnCompleteEvent evt)
{
    // Turn completed with 2 LLM calls
    // Total tokens: 300 input, 150 output
    // Total cost: $0.003750
}
```

## Benefits

### For Host Applications
- **Real-time cost monitoring:** Track costs as they happen
- **Budget management:** Set limits and alerts based on spending
- **Usage analytics:** Understand which models and operations are most expensive
- **Transparent pricing:** See exactly what each LLM call costs
- **Backward compatible:** Existing code continues to work

### For Developers
- **Debugging:** Track cost per response for optimization
- **Testing:** Monitor costs during development
- **Reporting:** Generate usage and cost reports
- **Multi-model comparison:** Compare costs across different providers

## Usage Examples

### Basic Cost Tracking
```csharp
public class CostObserver : IAgentObserver
{
    public void OnLlmResponse(LlmResponseEvent evt)
    {
        Console.WriteLine($"Model: {evt.ModelName}");
        Console.WriteLine($"Cost: ${evt.Usage.TotalCost:F6}");
        Console.WriteLine($"Tokens: {evt.Usage.TotalTokens}");
    }

    public void OnTurnComplete(TurnCompleteEvent evt)
    {
        Console.WriteLine($"Turn total: ${evt.Result.TotalCost:F4}");
    }
}
```

### Custom Cost Calculator
```csharp
builder.WithCostCalculator(new CustomCostCalculator())
       .WithObserver(new CostObserver());
```

## Pricing Notes

- **OpenAI:** Current API pricing as of Oct 2025
- **Anthropic:** Claude 4.5 and 3.7 series pricing
- **Google:** Gemini 2.5 pricing (updated rates)
- **XAI:** Grok 4 pricing (updated rates)
- **Groq/OpenRouter:** Returns $0 (pricing varies by tier/contract)

## Backward Compatibility

All changes are backward compatible:
- New fields have default values
- Existing observers continue to work
- No breaking changes to existing APIs
- Optional feature - can be ignored if not needed
