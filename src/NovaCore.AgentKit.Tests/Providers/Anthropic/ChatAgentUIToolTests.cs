using System.Text.Json;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Anthropic;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Anthropic;

/// <summary>
/// UI tool tests (human-in-the-loop) with Anthropic Claude
/// </summary>
public class ChatAgentUIToolTests : ProviderTestBase
{
    public ChatAgentUIToolTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task UIToolCall_PausesExecution()
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
            .AddUITool(new PaymentUITool())
            .WithSystemPrompt("You are a payment assistant. When user wants to pay, use the show_payment_page tool.")
            .BuildChatAgentAsync();
        
        // Act
        var response = await agent.SendAsync("I want to pay $99.99 USD");
        
        // Assert - Should pause with UI tool call
        Assert.NotNull(response);
        Assert.NotNull(response.ToolCalls);
        Assert.NotEmpty(response.ToolCalls);
        
        var toolCall = response.ToolCalls.First();
        Assert.Equal("show_payment_page", toolCall.FunctionName);
        
        // Parse arguments to verify
        var args = JsonSerializer.Deserialize<PaymentArgs>(toolCall.Arguments);
        Assert.NotNull(args);
        Assert.True(args.Amount > 0);
        Assert.Equal("USD", args.Currency);
        
        Output.WriteLine($"UI Tool called: {toolCall.FunctionName}");
        Output.WriteLine($"Arguments: {toolCall.Arguments}");
        Output.WriteLine($"Assistant response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task SendToolResult_ResumesExecution()
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
            .AddUITool(new PaymentUITool())
            .WithSystemPrompt("You are a payment assistant. Use show_payment_page tool when user wants to pay.")
            .BuildChatAgentAsync();
        
        // Act - Step 1: Trigger UI tool
        var response1 = await agent.SendAsync("I want to pay $50.00 USD");
        
        Assert.NotNull(response1.ToolCalls);
        var toolCall = response1.ToolCalls.First();
        
        Output.WriteLine($"[Paused] UI tool: {toolCall.FunctionName}");
        
        // Step 2: Simulate user completing payment in UI
        var paymentResult = new PaymentResult(
            Success: true,
            TransactionId: "TX-12345",
            Amount: 50.00m);
        
        var toolResultMessage = new ChatMessage(
            ChatRole.Tool,
            JsonSerializer.Serialize(paymentResult),
            toolCall.Id);
        
        // Step 3: Send result back to resume
        var response2 = await agent.SendAsync(toolResultMessage);
        
        // Assert - Should acknowledge payment completion
        Assert.NotNull(response2);
        Assert.NotNull(response2.Text);
        Assert.Contains("TX-12345", response2.Text, StringComparison.OrdinalIgnoreCase);
        
        Output.WriteLine($"[Resumed] Final response: {response2.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task PaymentFlow_WorksEndToEnd()
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
            .AddUITool(new PaymentUITool())
            .WithSystemPrompt("You are a payment assistant. Help users complete payments using the show_payment_page tool.")
            .BuildChatAgentAsync();
        
        // Act - Complete payment flow
        Output.WriteLine("=== Starting Payment Flow ===");
        
        // User asks to pay
        var response1 = await agent.SendAsync("I need to pay for my order - it's $249.99");
        Output.WriteLine($"Step 1 - Agent: {response1.Text}");
        
        if (response1.ToolCalls?.Any() == true)
        {
            var toolCall = response1.ToolCalls.First();
            Output.WriteLine($"Step 2 - UI Tool Triggered: {toolCall.FunctionName}");
            Output.WriteLine($"Step 2 - Arguments: {toolCall.Arguments}");
            
            // Simulate payment completion
            var paymentResult = new PaymentResult(
                Success: true,
                TransactionId: "TX-TEST-999",
                Amount: 249.99m);
            
            var toolResult = new ChatMessage(
                ChatRole.Tool,
                JsonSerializer.Serialize(paymentResult),
                toolCall.Id);
            
            var finalResponse = await agent.SendAsync(toolResult);
            Output.WriteLine($"Step 3 - Final: {finalResponse.Text}");
            
            // Assert
            Assert.Contains("TX-TEST-999", finalResponse.Text, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            throw new Exception("Expected UI tool call but got none");
        }
        
        await agent.DisposeAsync();
    }
}

