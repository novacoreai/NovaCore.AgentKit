using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Anthropic;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Anthropic;

/// <summary>
/// ChatAgent with internal tool execution tests
/// </summary>
public class ChatAgentWithToolsTests : ProviderTestBase
{
    public ChatAgentWithToolsTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task AgentCallsInternalTool_ExecutesAutomatically()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithObserver(Observer)
            .AddTool(new CalculatorTool())
            .WithSystemPrompt("You are a math assistant. Use the calculator tool for calculations.")
            .BuildChatAgentAsync();
        
        // Act
        var response = await agent.SendAsync("What is 42 multiplied by 17?");
        
        // Assert
        Assert.NotNull(response);
        Assert.Contains("714", response.Text);
        
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task MultipleToolCalls_ExecuteSequentially()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithObserver(Observer)
            .AddTool(new CalculatorTool())
            .WithSystemPrompt("You are a math assistant. Use the calculator tool for all calculations.")
            .BuildChatAgentAsync();
        
        // Act
        var response = await agent.SendAsync("Calculate 10 + 5, then multiply that result by 3");
        
        // Assert - Should get final answer (15 * 3 = 45)
        Assert.NotNull(response);
        Assert.Contains("45", response.Text);
        
        Output.WriteLine($"Response: {response.Text}");
        
        // Check history contains tool results
        var stats = agent.GetStats();
        Assert.True(stats.ToolMessages > 0, "Should have tool result messages");
        
        Output.WriteLine($"Tool messages in history: {stats.ToolMessages}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ToolResult_AppendedToHistory()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithObserver(Observer)
            .AddTool(new CalculatorTool())
            .WithSystemPrompt("You are a math assistant. Use the calculator tool.")
            .BuildChatAgentAsync();
        
        // Act
        await agent.SendAsync("Calculate 100 divided by 4");
        var stats = agent.GetStats();
        
        // Assert
        Assert.True(stats.TotalMessages >= 3, 
            $"Should have user + assistant(with tool call) + tool result, got {stats.TotalMessages}");
        Assert.True(stats.ToolMessages > 0, "Should have tool result messages");
        
        Output.WriteLine($"Total messages: {stats.TotalMessages}");
        Output.WriteLine($"Tool messages: {stats.ToolMessages}");
        
        await agent.DisposeAsync();
    }
}

