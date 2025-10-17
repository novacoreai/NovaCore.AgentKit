using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Anthropic;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Anthropic;

/// <summary>
/// ReActAgent tests with Anthropic Claude (autonomous task execution)
/// </summary>
public class ReActAgentBasicTests : ProviderTestBase
{
    public ReActAgentBasicTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task RunAsync_CompletesTask()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithLogger(Logger)
            .AddTool(new CalculatorTool())
            .WithReActConfig(cfg => cfg.MaxIterations = 10)
            .BuildReActAgentAsync();
        
        // Act
        var result = await agent.RunAsync("Calculate 25 * 8 and tell me the result");
        
        // Assert
        Assert.True(result.Success, $"Task failed: {result.Error}");
        Assert.NotNull(result.FinalAnswer);
        Assert.Contains("200", result.FinalAnswer);
        
        Output.WriteLine($"Success: {result.Success}");
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        Output.WriteLine($"Iterations: {result.Iterations.Count}");
        Output.WriteLine($"Tool calls: {result.TotalToolCalls}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task IterationsTracked_Correctly()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithLogger(Logger)
            .AddTool(new CalculatorTool())
            .WithReActConfig(cfg => cfg.MaxIterations = 5)
            .BuildReActAgentAsync();
        
        // Act
        var result = await agent.RunAsync("What is 100 divided by 4?");
        
        // Assert
        Assert.True(result.Iterations.Count > 0, "Should have at least 1 iteration");
        Assert.True(result.Iterations.Count <= 5, "Should not exceed max iterations");
        
        Output.WriteLine($"Iterations: {result.Iterations.Count}");
        foreach (var iteration in result.Iterations)
        {
            Output.WriteLine($"  Iteration {iteration.IterationNumber}: {iteration.ToolCallsExecuted} tool calls");
        }
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task CompleteTaskSignal_ReturnsResult()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithLogger(Logger)
            .WithReActConfig(cfg => cfg.MaxIterations = 10)
            .BuildReActAgentAsync();
        
        // Act - Simple task that doesn't require tools
        var result = await agent.RunAsync("What is the capital of France? Answer in one word.");
        
        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.FinalAnswer);
        Assert.Contains("Paris", result.FinalAnswer, StringComparison.OrdinalIgnoreCase);
        
        Output.WriteLine($"Final answer: {result.FinalAnswer}");
        Output.WriteLine($"Completed in {result.Iterations.Count} iterations");
        
        await agent.DisposeAsync();
    }
}

