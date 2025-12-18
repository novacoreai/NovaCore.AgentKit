using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Anthropic;
using NovaCore.AgentKit.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Anthropic;

/// <summary>
/// Basic ChatAgent tests with Anthropic Claude
/// </summary>
public class ChatAgentBasicTests : ProviderTestBase
{
    public ChatAgentBasicTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task SendMessage_ReturnsAssistantMessage()
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
            .WithSystemPrompt("You are a helpful assistant. Give brief, clear responses.")
            .BuildChatAgentAsync();
        
        // Act
        var response = await agent.SendAsync("What is 2+2?");
        
        // Assert
        Assert.NotNull(response);
        Assert.Equal(ChatRole.Assistant, response.Role);
        Assert.NotNull(response.Text);
        Assert.Contains("4", response.Text);
        
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task MultiTurnConversation_MaintainsContext()
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
            .WithSystemPrompt("You are a helpful assistant. Remember what users tell you.")
            .BuildChatAgentAsync();
        
        // Act
        var response1 = await agent.SendAsync("My favorite color is blue.");
        var response2 = await agent.SendAsync("What's my favorite color?");
        
        // Assert
        Assert.Contains("blue", response2.Text, StringComparison.OrdinalIgnoreCase);
        
        Output.WriteLine($"Turn 1: {response1.Text}");
        Output.WriteLine($"Turn 2: {response2.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task SendMessage_WithImage_Works()
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
            .WithSystemPrompt("You are a vision assistant. Describe what you see in images.")
            .BuildChatAgentAsync();
        
        // Act
        var image = await FileAttachment.FromFileAsync("files/test.png");
        var response = await agent.SendAsync("What's in this image?", new List<FileAttachment> { image });
        
        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Text);
        Assert.NotEmpty(response.Text);
        
        Output.WriteLine($"Vision response: {response.Text}");
        
        await agent.DisposeAsync();
    }
}

