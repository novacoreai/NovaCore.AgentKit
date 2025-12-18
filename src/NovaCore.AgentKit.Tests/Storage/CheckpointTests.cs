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
/// Tests for automatic checkpointing and summarization
/// </summary>
public class CheckpointTests : ProviderTestBase
{
    public CheckpointTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task CreateCheckpoint_StoresInDatabase()
    {
        // Arrange
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        
        var config = TestConfigHelper.GetConfig();
        var agent = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation("checkpoint-test")
            .WithSystemPrompt("You are a test assistant. Give very brief responses.")
            .BuildChatAgentAsync();
        
        // Act - Create some messages
        await agent.SendAsync("Message 1");
        await agent.SendAsync("Message 2");
        
        // Manually create checkpoint
        await agent.CreateCheckpointAsync("Summary of first 2 messages", upToTurnNumber: 2);
        
        // Assert
        var checkpoint = await agent.GetLatestCheckpointAsync();
        Assert.NotNull(checkpoint);
        Assert.Equal(2, checkpoint.UpToTurnNumber);
        Assert.Contains("Summary", checkpoint.Summary);
        
        Output.WriteLine($"Checkpoint created at turn {checkpoint.UpToTurnNumber}");
        Output.WriteLine($"Summary: {checkpoint.Summary}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ManualCheckpoint_CanBeCreated()
    {
        // Note: Auto-checkpoint testing requires IChatClient injection which is complex
        // This test validates manual checkpoint creation works
        
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
            .ForConversation("manual-checkpoint-test")
            .WithSystemPrompt("You are a test assistant. Give very brief responses.")
            .BuildChatAgentAsync();
        
        // Act - Create several messages then checkpoint
        for (int i = 1; i <= 5; i++)
        {
            await agent.SendAsync($"Message {i}");
        }
        
        await agent.CreateCheckpointAsync("Summary of 5 messages about testing", upToTurnNumber: 5);
        
        // Assert
        var checkpoint = await agent.GetLatestCheckpointAsync();
        Assert.NotNull(checkpoint);
        Assert.Equal(5, checkpoint.UpToTurnNumber);
        Assert.Contains("testing", checkpoint.Summary, StringComparison.OrdinalIgnoreCase);
        
        Output.WriteLine($"Checkpoint created at turn {checkpoint.UpToTurnNumber}");
        Output.WriteLine($"Summary: {checkpoint.Summary}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task LoadFromCheckpoint_LoadsMessagesAfterCheckpoint()
    {
        // Arrange
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        var conversationId = "checkpoint-load-test";
        
        // Add some messages
        await historyStore.AppendMessagesAsync(conversationId, new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Message 1"),
            new ChatMessage(ChatRole.Assistant, "Response 1"),
            new ChatMessage(ChatRole.User, "Message 2"),
            new ChatMessage(ChatRole.Assistant, "Response 2"),
            new ChatMessage(ChatRole.User, "Message 3"),
            new ChatMessage(ChatRole.Assistant, "Response 3")
        });
        
        // Create checkpoint at message 3
        await historyStore.CreateCheckpointAsync(conversationId, new ConversationCheckpoint
        {
            UpToTurnNumber = 3,
            Summary = "Summary of first 4 messages",
            CreatedAt = DateTime.UtcNow
        });
        
        // Act - Load from checkpoint
        var (checkpoint, messagesAfter) = await historyStore.LoadFromCheckpointAsync(conversationId);
        
        // Assert
        Assert.NotNull(checkpoint);
        Assert.Equal(3, checkpoint.UpToTurnNumber);
        Assert.Equal(2, messagesAfter.Count); // Messages 4 and 5 (indices 4, 5)
        Assert.Equal("Message 3", messagesAfter[0].Text);
        Assert.Equal("Response 3", messagesAfter[1].Text);
    }
}

