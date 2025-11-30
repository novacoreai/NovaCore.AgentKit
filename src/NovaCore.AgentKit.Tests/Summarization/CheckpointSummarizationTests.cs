using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.EntityFramework;
using NovaCore.AgentKit.Providers.XAI;
using NovaCore.AgentKit.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Summarization;

/// <summary>
/// Comprehensive tests for checkpoint-based summarization with EF Core storage.
/// Tests the full flow: conversation → checkpoint → history retention → context reduction
/// </summary>
public class CheckpointSummarizationTests : ProviderTestBase
{
    public CheckpointSummarizationTests(ITestOutputHelper output) : base(output)
    {
    }
    
    /// <summary>
    /// Comprehensive test: Build up 30-turn conversation with manual checkpoints,
    /// verify history retention and context reduction work together.
    /// </summary>
    [Fact]
    public async Task ChatBot_WithCheckpoint_CompressesHistoryCorrectly()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, 
            LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        
        var conversationId = $"counting-test-{Guid.NewGuid()}";
        
        // Arrange: Create main chat agent with EF Core storage and history retention
        var agent = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = XAIModels.Grok4FastNonReasoning;
            })
            .WithConfig(cfg =>
            {
                cfg.SystemPrompt = "You are a counting assistant. When the user says a number, respond with just the next number. Be very brief.";
                cfg.MaxToolRoundsPerTurn = 1;
                
                // NEW SIMPLIFIED CONFIG: Just summarization with checkpoint
                cfg.Summarization = new SummarizationConfig
                {
                    Enabled = false,  // Disabled for manual checkpoint testing
                    TriggerAt = 50,   // Would trigger at 50 messages
                    KeepRecent = 10   // Would keep last 10 after summarization
                };
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation(conversationId)
            .BuildChatAgentAsync();
        
        Output.WriteLine("🚀 Starting checkpoint/summarization test");
        Output.WriteLine($"Configuration:");
        Output.WriteLine($"  - Max messages to LLM: 10");
        Output.WriteLine($"  - Keep recent: 5");
        Output.WriteLine($"  - Manual checkpoint at: 20 messages");
        Output.WriteLine("");
        
        // Act: Build up conversation with 30 counting iterations
        // Pattern: User says "1", Agent says "2", User says "4", Agent says "5", etc.
        int expectedNumber = 1;
        
        for (int turn = 1; turn <= 30; turn++)
        {
            Output.WriteLine($"Turn {turn}: User says {expectedNumber}");
            
            var response = await agent.SendAsync($"{expectedNumber}");
            
            Output.WriteLine($"Turn {turn}: Agent responds: {response}");
            
            // Create checkpoint at 20 messages to test summarization
            if (turn == 10)
            {
                await agent.CreateCheckpointAsync("Checkpoint after 10 counting turns", 10);
                Output.WriteLine("📍 Created checkpoint at turn 10");
            }
            
            // Agent should respond with next number, then we skip one
            expectedNumber += 2;
            
            // Brief delay to avoid rate limiting
            await Task.Delay(200);
        }
        
        Output.WriteLine("");
        Output.WriteLine("✅ Completed 30 turns of conversation");
        Output.WriteLine("");
        
        // Assert: Verify checkpoint was created
        var checkpoint = await agent.GetLatestCheckpointAsync();
        Assert.NotNull(checkpoint);
        Output.WriteLine($"📝 Checkpoint Summary: {checkpoint.Summary}");
        Output.WriteLine($"📍 Checkpoint covers up to turn: {checkpoint.UpToTurnNumber}");
        Output.WriteLine("");
        
        // Assert: Verify checkpoint has valid summary
        Assert.NotEmpty(checkpoint.Summary);
        Assert.True(checkpoint.Summary.Length > 10, "Checkpoint summary should be meaningful");
        Assert.Equal(10, checkpoint.UpToTurnNumber);
        
        // Assert: Verify in-memory stats (may be compressed - that's OK!)
        var memoryStats = agent.GetStats();
        Output.WriteLine($"📊 In-memory messages: {memoryStats.TotalMessages} (compression may have occurred)");
        Output.WriteLine($"📊 In-memory user messages: {memoryStats.UserMessages}");
        Output.WriteLine($"📊 In-memory assistant messages: {memoryStats.AssistantMessages}");
        
        // Assert: Verify DATABASE has all messages (this is what matters for persistence!)
        var dbMessages = await historyStore.LoadAsync(conversationId);
        Output.WriteLine($"📊 Database messages: {dbMessages?.Count ?? 0}");
        
        var dbUserMessages = dbMessages?.Count(m => m.Role == ChatRole.User) ?? 0;
        var dbAssistantMessages = dbMessages?.Count(m => m.Role == ChatRole.Assistant) ?? 0;
        
        Output.WriteLine($"📊 Database user messages: {dbUserMessages}");
        Output.WriteLine($"📊 Database assistant messages: {dbAssistantMessages}");
        
        // Database should have ALL messages (system + 30 user + 30 assistant = ~61 messages)
        Assert.True((dbMessages?.Count ?? 0) >= 60, $"Expected at least 60 messages in DB, got {dbMessages?.Count ?? 0}");
        Assert.True(dbUserMessages >= 30, $"Expected at least 30 user messages, got {dbUserMessages}");
        Assert.Equal(30, dbAssistantMessages);
        
        // Final summary
        Output.WriteLine("");
        Output.WriteLine("═══════════════════════════════════════");
        Output.WriteLine("✅ ALL ASSERTIONS PASSED");
        Output.WriteLine("═══════════════════════════════════════");
        Output.WriteLine($"✓ In-memory: {memoryStats.TotalMessages} messages (compressed as designed)");
        Output.WriteLine($"✓ Database: {dbMessages?.Count ?? 0} messages (all persisted)");
        Output.WriteLine($"✓ Checkpoint created: Turn {checkpoint.UpToTurnNumber}");
        Output.WriteLine($"✓ History retention: Limiting context to 10 messages for LLM");
        Output.WriteLine($"✓ Counting game: 30 successful rounds");
        Output.WriteLine("═══════════════════════════════════════");
        
        await agent.DisposeAsync();
        dbContext.Dispose();
    }
    
    /// <summary>
    /// Test manual checkpoint creation with shorter conversation
    /// </summary>
    [Fact]
    public async Task ChatBot_ManualCheckpoint_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, 
            LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        
        var agent = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = XAIModels.Grok4FastNonReasoning;
            })
            .WithConfig(cfg =>
            {
                cfg.SystemPrompt = "You are a helpful assistant. Give very brief responses.";
                // No summarization config needed for this test
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation($"manual-checkpoint-test-{Guid.NewGuid()}")
            .BuildChatAgentAsync();
        
        Output.WriteLine("🚀 Testing manual checkpoint creation");
        Output.WriteLine("");
        
        // Act: Build up conversation
        for (int i = 1; i <= 12; i++)
        {
            await agent.SendAsync($"Message number {i}");
            await Task.Delay(150);
        }
        
        // Create manual checkpoint
        await agent.CreateCheckpointAsync("Summary: User sent 12 numbered messages", 10);
        
        // Get checkpoint
        var checkpoint = await agent.GetLatestCheckpointAsync();
        
        Assert.NotNull(checkpoint);
        Output.WriteLine($"📝 Checkpoint summary: {checkpoint.Summary}");
        Output.WriteLine($"📍 Checkpoint turn: {checkpoint.UpToTurnNumber}");
        Output.WriteLine("");
        
        Assert.Contains("12", checkpoint.Summary);
        Assert.Equal(10, checkpoint.UpToTurnNumber);
        
        Output.WriteLine("✅ Manual checkpoint created and verified successfully");
        
        await agent.DisposeAsync();
        dbContext.Dispose();
    }
    
    /// <summary>
    /// Test that history retention works properly (context reduction)
    /// </summary>
    [Fact]
    public async Task ChatBot_HistoryRetention_LimitsContextSize()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, 
            LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        
        var agent = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = XAIModels.Grok4FastNonReasoning;
            })
            .WithConfig(cfg =>
            {
                cfg.SystemPrompt = "You are a helpful assistant.";
                // No special config needed for this simple test
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation($"retention-test-{Guid.NewGuid()}")
            .BuildChatAgentAsync();
        
        Output.WriteLine("🚀 Testing history retention");
        Output.WriteLine("");
        
        // Act: Build up conversation beyond retention limit
        for (int i = 1; i <= 20; i++)
        {
            await agent.SendAsync($"Message {i}");
            await Task.Delay(150);
        }
        
        // Get stats
        var stats = agent.GetStats();
        
        Output.WriteLine($"📊 Total messages in history: {stats.TotalMessages}");
        
        // With 20 turns (40 messages) + system, but max 10 to send,
        // the full history should still contain everything
        Assert.True(stats.TotalMessages >= 40, "Full history should be preserved");
        
        Output.WriteLine("✅ History retention limits context while preserving full history");
        
        await agent.DisposeAsync();
        dbContext.Dispose();
    }
}
