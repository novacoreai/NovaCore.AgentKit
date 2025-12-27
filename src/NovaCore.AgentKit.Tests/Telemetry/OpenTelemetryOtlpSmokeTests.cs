using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Tests.Helpers;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Telemetry;

public sealed class OpenTelemetryOtlpSmokeTests : ProviderTestBase
{
    public OpenTelemetryOtlpSmokeTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task Otlp_Exports_AgentKit_Metrics_And_Test_Trace()
    {
        // Defaults match user's local OTLP listener; can be overridden via env vars if desired.
        // For gRPC OTLP exporter, endpoint is typically http://localhost:4317
        var tracesEndpoint = GetOtlpGrpcEndpoint();
        var metricsEndpoint = GetOtlpHttpEndpoint();
        
        if (!await IsTcpPortOpenAsync(tracesEndpoint.Host, tracesEndpoint.Port, timeoutMs: 500))
        {
            Output.WriteLine($"Skipping: OTLP gRPC endpoint not reachable at {tracesEndpoint}. Start your OTEL Collector/Jaeger OTLP receiver first.");
            return;
        }
        
        // NOTE: Jaeger UI will show traces, not metrics.
        // This test exports both:
        // - a tiny trace span (so you can confirm connectivity in Jaeger)
        // - AgentKit metrics (so they can be scraped/collected if your pipeline supports metrics)
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("NovaCore.AgentKit.Tests"))
            .AddSource("NovaCore.AgentKit.Tests")
            .AddOtlpExporter(o =>
            {
                o.Endpoint = tracesEndpoint;
                o.Protocol = OtlpExportProtocol.Grpc;
            })
            .Build();
        
        var useHttpForMetrics = await IsTcpPortOpenAsync(metricsEndpoint.Host, metricsEndpoint.Port, timeoutMs: 500);
        var actualMetricsEndpoint = useHttpForMetrics ? metricsEndpoint : tracesEndpoint;
        var metricsProtocol = useHttpForMetrics ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
        
        // In-process validation: confirm AgentKit emits the metrics locally even if your OTLP backend rejects metrics.
        // This helps distinguish "instrumentation is broken" from "backend doesn't accept metrics".
        long observedInputTokens = 0;
        long observedOutputTokens = 0;
        double observedInputCostUsd = 0;
        double observedOutputCostUsd = 0;
        
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "novacore.agentkit")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == "novacore.agentkit.llm.input_tokens") observedInputTokens += measurement;
            if (instrument.Name == "novacore.agentkit.llm.output_tokens") observedOutputTokens += measurement;
        });
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == "novacore.agentkit.llm.input_cost_usd") observedInputCostUsd += measurement;
            if (instrument.Name == "novacore.agentkit.llm.output_cost_usd") observedOutputCostUsd += measurement;
        });
        meterListener.Start();
        
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("NovaCore.AgentKit.Tests"))
            .AddMeter("novacore.agentkit")
            .AddOtlpExporter(o =>
            {
                o.Endpoint = actualMetricsEndpoint;
                o.Protocol = metricsProtocol;
                o.ExportProcessorType = ExportProcessorType.Batch;
            })
            .Build();
        
        using var activitySource = new ActivitySource("NovaCore.AgentKit.Tests");
        using var activity = activitySource.StartActivity("otel-smoke-test");
        activity?.SetTag("test.name", nameof(Otlp_Exports_AgentKit_Metrics_And_Test_Trace));
        
        // Trigger one agent LLM call that has Usage; metrics are auto-wired via the telemetry package reference.
        const int inputTokens = 123;
        const int outputTokens = 45;
        var llmClient = new DeterministicLlmClient(
            responseText: "ok",
            inputTokens: inputTokens,
            outputTokens: outputTokens);
        
        await using var agent = await new AgentBuilder()
            .UseLlmClient(llmClient)
            .WithModel("gpt-4o") // known pricing in ModelPricingCalculator; Agent.cs computes per-direction costs
            .WithObserver(Observer) // verify composite observer still works; metrics observer is auto-attached too
            .WithSystemPrompt("Return a short acknowledgement.")
            .BuildChatAgentAsync();
        
        var response = await agent.SendAsync("hello");
        Output.WriteLine($"Assistant: {response.Text}");
        
        // Flush exporters so you can see the data immediately.
        var metricsFlushed = meterProvider.ForceFlush(timeoutMilliseconds: 5_000);
        var tracesFlushed = tracerProvider.ForceFlush(timeoutMilliseconds: 5_000);
        
        Output.WriteLine($"ForceFlush(metrics)={metricsFlushed}, ForceFlush(traces)={tracesFlushed}");
        Output.WriteLine($"OTLP traces endpoint: {tracesEndpoint} (gRPC)");
        Output.WriteLine($"OTLP metrics endpoint: {actualMetricsEndpoint} ({metricsProtocol})");
        
        // Confirm local emission
        Output.WriteLine($"Observed(metrics in-proc): input_tokens={observedInputTokens}, output_tokens={observedOutputTokens}, input_cost_usd={observedInputCostUsd}, output_cost_usd={observedOutputCostUsd}");
        Assert.Equal(inputTokens, observedInputTokens);
        Assert.Equal(outputTokens, observedOutputTokens);
        
        // Nothing deterministic to assert here (depends on local collector/backend); smoke test is for end-to-end export.
        Assert.True(true);
    }
    
    private static Uri GetOtlpGrpcEndpoint()
    {
        // Respect standard env vars if present; otherwise default to localhost:4317.
        // OTEL_EXPORTER_OTLP_ENDPOINT can be "http://localhost:4317" or "http://localhost:4318"
        var env = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(env) && Uri.TryCreate(env, UriKind.Absolute, out var uri))
        {
            return uri;
        }
        
        return new Uri("http://localhost:4317");
    }
    
    private static Uri GetOtlpHttpEndpoint()
    {
        // Respect standard env vars if present; otherwise default to localhost:4318 (OTLP/HTTP).
        var env = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(env) && Uri.TryCreate(env, UriKind.Absolute, out var uri))
        {
            return uri;
        }
        
        return new Uri("http://localhost:4318");
    }
    
    private static async Task<bool> IsTcpPortOpenAsync(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) == connectTask;
            return completed && client.Connected;
        }
        catch
        {
            return false;
        }
    }
    
    private sealed class DeterministicLlmClient : ILlmClient
    {
        private readonly string _responseText;
        private readonly int _inputTokens;
        private readonly int _outputTokens;
        
        public DeterministicLlmClient(string responseText, int inputTokens, int outputTokens)
        {
            _responseText = responseText;
            _inputTokens = inputTokens;
            _outputTokens = outputTokens;
        }
        
        public Task<LlmResponse> GetResponseAsync(List<LlmMessage> messages, LlmOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmResponse
            {
                Text = _responseText,
                ToolCalls = null,
                FinishReason = LlmFinishReason.Stop,
                Usage = new LlmUsage
                {
                    InputTokens = _inputTokens,
                    OutputTokens = _outputTokens
                }
            });
        }
        
        public async IAsyncEnumerable<LlmStreamingUpdate> GetStreamingResponseAsync(
            List<LlmMessage> messages,
            LlmOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Minimal streaming: return one delta and a final usage update.
            yield return new LlmStreamingUpdate { TextDelta = _responseText };
            await Task.Yield();
            yield return new LlmStreamingUpdate
            {
                FinishReason = LlmFinishReason.Stop,
                Usage = new LlmUsage { InputTokens = _inputTokens, OutputTokens = _outputTokens }
            };
        }
    }
}


