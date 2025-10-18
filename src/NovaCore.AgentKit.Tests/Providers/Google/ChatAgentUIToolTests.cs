using System.Text.Json;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Google;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Google;

public class ChatAgentUIToolTests : ProviderTestBase
{
    public ChatAgentUIToolTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task UIToolCall_PausesExecution()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithObserver(Observer)
            .AddUITool(new PaymentUITool())
            .WithSystemPrompt("You are a payment assistant. When user mentions paying or wants to make a payment, you MUST immediately call the show_payment_page tool with the amount and currency. Do not ask for additional information.")
            .BuildChatAgentAsync();
        
        var response = await agent.SendAsync("I need to pay $99.99 USD");
        
        Assert.NotNull(response.ToolCalls);
        Assert.Equal("show_payment_page", response.ToolCalls.First().FunctionName);
        
        Output.WriteLine($"UI Tool: {response.ToolCalls.First().FunctionName}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task SendToolResult_ResumesExecution()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithObserver(Observer)
            .AddUITool(new PaymentUITool())
            .WithSystemPrompt("You are a payment assistant. When user mentions paying, MUST call show_payment_page tool immediately. When you receive payment confirmation with a transaction ID, always mention the transaction ID in your response.")
            .BuildChatAgentAsync();
        
        var response1 = await agent.SendAsync("I need to pay $50.00 USD");
        var toolCall = response1.ToolCalls!.First();
        
        var paymentResult = new PaymentResult(true, "TX-12345", 50.00m);
        var toolResult = new ChatMessage(ChatRole.Tool, JsonSerializer.Serialize(paymentResult), toolCall.Id);
        var response2 = await agent.SendAsync(toolResult);
        
        Assert.Contains("TX-12345", response2.Text, StringComparison.OrdinalIgnoreCase);
        Output.WriteLine($"Final: {response2.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task PaymentFlow_WorksEndToEnd()
    {
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithObserver(Observer)
            .AddUITool(new PaymentUITool())
            .WithSystemPrompt("You are a payment assistant. When user mentions paying, MUST call show_payment_page tool immediately. When you receive payment confirmation with a transaction ID, always mention the transaction ID in your response.")
            .BuildChatAgentAsync();
        
        var response1 = await agent.SendAsync("I need to pay $249.99 USD");
        
        if (response1.ToolCalls?.Any() == true)
        {
            var toolCall = response1.ToolCalls.First();
            var result = new PaymentResult(true, "TX-999", 249.99m);
            var toolResult = new ChatMessage(ChatRole.Tool, JsonSerializer.Serialize(result), toolCall.Id);
            var finalResponse = await agent.SendAsync(toolResult);
            
            Assert.Contains("TX-999", finalResponse.Text, StringComparison.OrdinalIgnoreCase);
        }
        
        await agent.DisposeAsync();
    }
}
