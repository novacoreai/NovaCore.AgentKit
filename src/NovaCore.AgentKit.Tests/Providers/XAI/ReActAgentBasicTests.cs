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
            .WithObserver(Observer)
            .AddTool(new CalculatorTool())
            .WithReActConfig(cfg => cfg.MaxTurns = 10)
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("Calculate 25 * 8");
        
        // Assert
        Output.WriteLine($"Success: {result.Success}");
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        Output.WriteLine($"Turns: {result.TurnsExecuted}, LLM calls: {result.TotalLlmCalls}");
        
        Assert.True(result.Success, "Task should complete successfully");
        Assert.False(string.IsNullOrWhiteSpace(result.FinalAnswer), $"FinalAnswer should not be empty. Got: '{result.FinalAnswer}'");
        Assert.Contains("200", result.FinalAnswer);
        
        await agent.DisposeAsync();
    }
}

