using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Google;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Google;

public class ChatAgentWithToolsTests : ProviderTestBase
{
    public ChatAgentWithToolsTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task AgentCallsInternalTool_ExecutesAutomatically()
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
            .WithSystemPrompt("You are a math assistant. Use the calculator tool.")
            .BuildChatAgentAsync();
        
        var response = await agent.SendAsync("What is 42 multiplied by 17?");
        
        Assert.Contains("714", response.Text);
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task MultipleToolCalls_ExecuteSequentially()
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
            .WithSystemPrompt("You are a math assistant. Use the calculator tool for all calculations.")
            .BuildChatAgentAsync();
        
        var response = await agent.SendAsync("Calculate 10 + 5, then multiply that result by 3");
        
        Assert.Contains("45", response.Text);
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ToolResult_AppendedToHistory()
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
            .WithSystemPrompt("You are a math assistant. Use the calculator tool.")
            .BuildChatAgentAsync();
        
        await agent.SendAsync("Calculate 100 divided by 4");
        var stats = agent.GetStats();
        
        Assert.True(stats.ToolMessages > 0);
        Output.WriteLine($"Tool messages: {stats.ToolMessages}");
        
        await agent.DisposeAsync();
    }
}
