using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.XAI;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.XAI;

public class ChatAgentWithToolsTests : ProviderTestBase
{
    public ChatAgentWithToolsTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task AgentCallsInternalTool_ExecutesAutomatically()
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
            .WithSystemPrompt("You are a math assistant. Use the calculator tool.")
            .BuildChatAgentAsync();
        
        var response = await agent.SendAsync("What is 42 multiplied by 17?");
        
        Assert.Contains("714", response.Text);
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ToolResult_AppendedToHistory()
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
            .BuildChatAgentAsync();
        
        await agent.SendAsync("Calculate 100 / 4");
        var stats = agent.GetStats();
        
        Assert.True(stats.ToolMessages > 0);
        Output.WriteLine($"Tool messages: {stats.ToolMessages}");
        
        await agent.DisposeAsync();
    }
}

