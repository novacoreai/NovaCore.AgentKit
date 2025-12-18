using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Google;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Google;

public class ReActAgentBasicTests : ProviderTestBase
{
    public ReActAgentBasicTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task RunAsync_CompletesTask()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithObserver(Observer)
            .AddTool(new CalculatorTool())
            .WithReActConfig(cfg => cfg.MaxTurns = 10)
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("Calculate 25 * 8 and tell me the result");
        
        Assert.True(result.Success);
        Assert.Contains("200", result.FinalAnswer);
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task IterationsTracked_Correctly()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithObserver(Observer)
            .AddTool(new CalculatorTool())
            .WithReActConfig(cfg => cfg.MaxTurns = 5)
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("What is 100 divided by 4?");
        
        Assert.True(result.TurnsExecuted > 0);
        Assert.True(result.TurnsExecuted <= 5);
        Output.WriteLine($"Iterations: {result.TurnsExecuted}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task CompleteTaskSignal_ReturnsResult()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithObserver(Observer)
            .WithReActConfig(cfg => cfg.MaxTurns = 10)
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("What is the capital of France? Answer in one word.");
        
        Assert.True(result.Success);
        Assert.Contains("Paris", result.FinalAnswer, StringComparison.OrdinalIgnoreCase);
        Output.WriteLine($"Final answer: {result.FinalAnswer}");
        
        await agent.DisposeAsync();
    }
}

