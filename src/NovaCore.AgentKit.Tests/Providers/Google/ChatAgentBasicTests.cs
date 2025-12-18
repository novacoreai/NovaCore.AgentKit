using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Google;
using NovaCore.AgentKit.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Google;

public class ChatAgentBasicTests : ProviderTestBase
{
    public ChatAgentBasicTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task SendMessage_ReturnsAssistantMessage()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithObserver(Observer)
            .WithSystemPrompt("You are a helpful assistant. Give brief, clear responses.")
            .BuildChatAgentAsync();
        
        var response = await agent.SendAsync("What is 2+2?");
        
        Assert.NotNull(response);
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
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithObserver(Observer)
            .WithSystemPrompt("You are a helpful assistant. Remember what users tell you.")
            .BuildChatAgentAsync();
        
        await agent.SendAsync("My favorite color is blue.");
        var response = await agent.SendAsync("What's my favorite color?");
        
        Assert.Contains("blue", response.Text, StringComparison.OrdinalIgnoreCase);
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task SendMessage_WithImage_Works()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithObserver(Observer)
            .WithSystemPrompt("You are a vision assistant. Describe what you see.")
            .BuildChatAgentAsync();
        
        var image = await FileAttachment.FromFileAsync("files/test.png");
        var response = await agent.SendAsync("What's in this image?", new List<FileAttachment> { image });
        
        Assert.NotNull(response.Text);
        Output.WriteLine($"Vision response: {response.Text}");
        
        await agent.DisposeAsync();
    }
}

