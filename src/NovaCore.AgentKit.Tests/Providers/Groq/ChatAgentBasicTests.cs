using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Groq;
using NovaCore.AgentKit.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Groq;

public class ChatAgentBasicTests : ProviderTestBase
{
    public ChatAgentBasicTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task SendMessage_ReturnsAssistantMessage()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGroq(options =>
            {
                options.ApiKey = config.Providers.Groq.ApiKey;
                options.Model = config.Providers.Groq.Model;
            })
            .WithLogger(Logger)
            .WithSystemPrompt("You are a helpful assistant. Give brief responses.")
            .BuildChatAgentAsync();
        
        var response = await agent.SendAsync("What is 2+2?");
        
        Assert.Equal(ChatRole.Assistant, response.Role);
        Assert.Contains("4", response.Text);
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task MultiTurnConversation_MaintainsContext()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGroq(options =>
            {
                options.ApiKey = config.Providers.Groq.ApiKey;
                options.Model = config.Providers.Groq.Model;
            })
            .WithLogger(Logger)
            .WithSystemPrompt("You are a helpful assistant. Remember what users tell you.")
            .BuildChatAgentAsync();
        
        await agent.SendAsync("My favorite color is blue.");
        var response = await agent.SendAsync("What's my favorite color?");
        
        Assert.Contains("blue", response.Text, StringComparison.OrdinalIgnoreCase);
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
}

