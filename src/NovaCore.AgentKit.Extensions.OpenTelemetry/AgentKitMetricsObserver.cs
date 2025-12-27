using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Extensions.OpenTelemetry;

/// <summary>
/// Metrics-only OpenTelemetry instrumentation for NovaCore.AgentKit.
///
/// This observer records per-LLM-call (granular) deltas for:
/// - input tokens
/// - output tokens
/// - input cost (USD)
/// - output cost (USD)
///
/// The host app must configure an OpenTelemetry Metrics pipeline/exporter and
/// listen to the Meter named "novacore.agentkit".
/// </summary>
public sealed class AgentKitMetricsObserver : IAgentObserver
{
    public const string MeterName = "novacore.agentkit";
    
    // Prefer a stable host-app dimension without requiring host configuration.
    private readonly string _hostAppName;
    
    // Static instruments (cheap + safe). No-ops if no MeterListener/OTel pipeline is attached.
    private static readonly Meter Meter = new(MeterName);
    
    private static readonly Counter<long> InputTokens = Meter.CreateCounter<long>(
        name: "novacore.agentkit.llm.input_tokens",
        unit: "token",
        description: "Input tokens used by LLM calls");
    
    private static readonly Counter<long> OutputTokens = Meter.CreateCounter<long>(
        name: "novacore.agentkit.llm.output_tokens",
        unit: "token",
        description: "Output tokens used by LLM calls");
    
    private static readonly Counter<double> InputCostUsd = Meter.CreateCounter<double>(
        name: "novacore.agentkit.llm.input_cost_usd",
        unit: "USD",
        description: "Input cost (USD) for LLM calls (based on AgentKit model pricing)");
    
    private static readonly Counter<double> OutputCostUsd = Meter.CreateCounter<double>(
        name: "novacore.agentkit.llm.output_cost_usd",
        unit: "USD",
        description: "Output cost (USD) for LLM calls (based on AgentKit model pricing)");
    
    public AgentKitMetricsObserver()
    {
        _hostAppName = Assembly.GetEntryAssembly()?.GetName().Name
            ?? AppDomain.CurrentDomain.FriendlyName
            ?? "unknown";
    }
    
    public void OnLlmResponse(LlmResponseEvent evt)
    {
        var usage = evt.Usage;
        if (usage == null)
        {
            return;
        }
        
        // Heuristic: if tokens > 0 but cost == 0, pricing is likely unknown (model not in table).
        var pricingKnown = usage.TotalTokens == 0 || usage.TotalCost != 0m;
        
        var model = string.IsNullOrWhiteSpace(evt.ModelName) ? "unknown" : evt.ModelName;
        
        var tags = new TagList
        {
            { "model", model },
            { "host_app", _hostAppName },
            { "pricing_known", pricingKnown }
        };
        
        if (usage.InputTokens > 0)
        {
            InputTokens.Add(usage.InputTokens, tags);
        }
        
        if (usage.OutputTokens > 0)
        {
            OutputTokens.Add(usage.OutputTokens, tags);
        }
        
        // Costs are recorded as deltas; Grafana/Prometheus can sum/roll up over time windows.
        if (usage.InputCost != 0m)
        {
            InputCostUsd.Add((double)usage.InputCost, tags);
        }
        
        if (usage.OutputCost != 0m)
        {
            OutputCostUsd.Add((double)usage.OutputCost, tags);
        }
    }
}


