using System.Text.Json;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Groq;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Groq;

public class ChatAgentUIToolTests : ProviderTestBase
{
    public ChatAgentUIToolTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task UIToolCall_PausesExecution()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGroq(options =>
            {
                options.ApiKey = config.Providers.Groq.ApiKey;
                options.Model = config.Providers.Groq.Model;
            })
            .WithObserver(Observer)
            .AddUITool(new PaymentUITool())
            .WithSystemPrompt("You are a payment assistant. Use show_payment_page tool.")
            .BuildChatAgentAsync();
        
        var response = await agent.SendAsync("I want to pay $99.99");
        
        var toolCalls = response.ToolCalls ?? throw new InvalidOperationException("Expected the agent to request the payment UI tool.");
        Assert.Equal("show_payment_page", toolCalls.First().FunctionName);
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task PaymentFlow_WorksEndToEnd()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGroq(options =>
            {
                options.ApiKey = config.Providers.Groq.ApiKey;
                options.Model = config.Providers.Groq.Model;
            })
            .WithObserver(Observer)
            .AddUITool(new PaymentUITool())
            .WithSystemPrompt("You are a payment assistant. When you receive payment confirmation with a transaction ID, always mention the transaction ID in your response.")
            .BuildChatAgentAsync();
        
        var response1 = await agent.SendAsync("I need to pay $249.99");
        
        if (response1.ToolCalls?.Any() == true)
        {
            var toolCall = response1.ToolCalls.First();
            var result = new PaymentResult(true, "TX-GROQ", 249.99m);
            var toolResult = new ChatMessage(ChatRole.Tool, JsonSerializer.Serialize(result), toolCall.Id);
            var finalResponse = await agent.SendAsync(toolResult);
            var finalText = finalResponse.Text ?? throw new InvalidOperationException("Expected assistant to respond with text.");
            
            // Normalize Unicode characters (model may use non-breaking hyphen U+2011)
            var normalizedText = finalText.Replace('\u2011', '-');
            Assert.Contains("TX-GROQ", normalizedText, StringComparison.OrdinalIgnoreCase);
            Output.WriteLine($"Final Response: {finalResponse.Text}");
        }
        
        await agent.DisposeAsync();
    }
}

