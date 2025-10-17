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
            .WithLogger(Logger)
            .AddTool(new CalculatorTool())
            .WithReActConfig(cfg => cfg.MaxIterations = 10)
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
            .WithLogger(Logger)
            .AddTool(new CalculatorTool())
            .WithReActConfig(cfg => cfg.MaxIterations = 5)
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("What is 100 divided by 4?");
        
        Assert.True(result.Iterations.Count > 0);
        Assert.True(result.Iterations.Count <= 5);
        Output.WriteLine($"Iterations: {result.Iterations.Count}");
        
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
            .WithLogger(Logger)
            .WithReActConfig(cfg => cfg.MaxIterations = 10)
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("What is the capital of France? Answer in one word.");
        
        Assert.True(result.Success);
        Assert.Contains("Paris", result.FinalAnswer, StringComparison.OrdinalIgnoreCase);
        Output.WriteLine($"Final answer: {result.FinalAnswer}");
        
        await agent.DisposeAsync();
    }
}

