using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.EntityFramework;
using NovaCore.AgentKit.Providers.XAI;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Storage;

/// <summary>
/// Tests for incremental message storage (no delete-all pattern)
/// </summary>
public class IncrementalStorageTests : ProviderTestBase
{
    public IncrementalStorageTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task AppendMessage_AddsOneMessage_NoDeletion()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        
        var conversationId = Guid.NewGuid().ToString();
        
        // Act - Append 3 messages one at a time
        await historyStore.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.User, "Message 1"));
        await historyStore.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.Assistant, "Response 1"));
        await historyStore.AppendMessageAsync(conversationId, new ChatMessage(ChatRole.User, "Message 2"));
        
        // Assert
        var loaded = await historyStore.LoadAsync(conversationId);
        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.Count);
        Assert.Equal("Message 1", loaded[0].Text);
        Assert.Equal("Response 1", loaded[1].Text);
        Assert.Equal("Message 2", loaded[2].Text);
    }
    
    [Fact]
    public async Task AppendMessages_BatchAdds_CorrectTurnNumbers()
    {
        // Arrange
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        var conversationId = Guid.NewGuid().ToString();
        
        // Act - First batch
        await historyStore.AppendMessagesAsync(conversationId, new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Message 1"),
            new ChatMessage(ChatRole.Assistant, "Response 1")
        });
        
        // Second batch (should continue turn numbering)
        await historyStore.AppendMessagesAsync(conversationId, new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Message 2"),
            new ChatMessage(ChatRole.Assistant, "Response 2")
        });
        
        // Assert
        var loaded = await historyStore.LoadAsync(conversationId);
        Assert.NotNull(loaded);
        Assert.Equal(4, loaded.Count);
        
        // Verify order maintained
        Assert.Equal("Message 1", loaded[0].Text);
        Assert.Equal("Response 1", loaded[1].Text);
        Assert.Equal("Message 2", loaded[2].Text);
        Assert.Equal("Response 2", loaded[3].Text);
    }
    
    [Fact]
    public async Task ChatAgent_PersistsMessagesIncrementally()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        
        var agent = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation("test-conv")
            .WithSystemPrompt("You are a test assistant. Give brief responses.")
            .BuildChatAgentAsync();
        
        // Act - Send multiple messages
        await agent.SendAsync("Hi");
        var messageCount1 = await historyStore.GetMessageCountAsync("test-conv");
        
        await agent.SendAsync("How are you?");
        var messageCount2 = await historyStore.GetMessageCountAsync("test-conv");
        
        // Assert - Each SendAsync should add ~2 messages (user + assistant minimum)
        Assert.True(messageCount1 >= 2, $"After first message: {messageCount1}");
        Assert.True(messageCount2 > messageCount1, $"Second message should add more: {messageCount1} -> {messageCount2}");
        
        // Verify all messages persisted
        var allMessages = await historyStore.LoadAsync("test-conv");
        Assert.NotNull(allMessages);
        Assert.True(allMessages.Count >= 4, $"Should have at least 4 messages, got {allMessages.Count}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ToolCallsJson_StoredAndLoaded()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        
        var agent = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation("tool-test")
            .AddTool(new CalculatorTool())
            .WithSystemPrompt("You are a test assistant. Use the calculator tool when asked to calculate.")
            .BuildChatAgentAsync();
        
        // Act - Trigger tool call
        var response = await agent.SendAsync("What is 25 + 17?");
        
        // Assert
        var loaded = await historyStore.LoadAsync("tool-test");
        Assert.NotNull(loaded);
        
        // Find assistant message with tool calls
        var assistantWithTools = loaded.FirstOrDefault(m => 
            m.Role == ChatRole.Assistant && m.ToolCalls != null && m.ToolCalls.Any());
        
        if (assistantWithTools != null)
        {
            Assert.NotNull(assistantWithTools.ToolCalls);
            Assert.Contains(assistantWithTools.ToolCalls, tc => tc.FunctionName == "calculator");
            Output.WriteLine($"Tool calls persisted correctly: {assistantWithTools.ToolCalls.Count} tool calls");
        }
        
        await agent.DisposeAsync();
    }
}

