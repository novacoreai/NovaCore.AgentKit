using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.XAI;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.XAI;

public class ReActAgentBasicTests : ProviderTestBase
{
    public ReActAgentBasicTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task RunAsync_CompletesTask()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithLogger(Logger)
            .WithLoggerFactory(LoggerFactory)
            .WithLogging(cfg =>
            {
                cfg.LogToolCallRequests = LogVerbosity.Full;
                cfg.LogToolCallResponses = LogVerbosity.Full;
            })
            .AddTool(new CalculatorTool())
            .WithReActConfig(cfg => cfg.MaxIterations = 10)
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("Calculate 25 * 8");
        
        Logger.LogInformation("=== Test Result ===");
        Logger.LogInformation("Success: {Success}", result.Success);
        Logger.LogInformation("FinalAnswer: '{Answer}'", result.FinalAnswer);
        Logger.LogInformation("FinalAnswer Length: {Length}", result.FinalAnswer?.Length ?? 0);
        
        Assert.True(result.Success, "Task should complete successfully");
        Assert.False(string.IsNullOrWhiteSpace(result.FinalAnswer), $"FinalAnswer should not be empty. Got: '{result.FinalAnswer}'");
        Assert.Contains("200", result.FinalAnswer);
        Output.WriteLine($"Answer: {result.FinalAnswer}, Iterations: {result.Iterations.Count}");
        
        await agent.DisposeAsync();
    }
}

