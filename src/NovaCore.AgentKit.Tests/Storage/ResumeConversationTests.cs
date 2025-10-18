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
/// Tests for conversation resume capability (critical new feature)
/// </summary>
public class ResumeConversationTests : ProviderTestBase
{
    public ResumeConversationTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task BuildChatAgentAsync_LoadsExistingHistory()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        var conversationId = "resume-test-1";
        
        // Session 1: Create conversation
        var agent1 = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation(conversationId)
            .WithSystemPrompt("You are a test assistant. Remember what users tell you.")
            .BuildChatAgentAsync();
        
        await agent1.SendAsync("My name is Alice.");
        var stats1 = agent1.GetStats();
        await agent1.DisposeAsync();
        
        Output.WriteLine($"Session 1: {stats1.TotalMessages} messages");
        
        // Act - Session 2: Resume conversation (same ID)
        var agent2 = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation(conversationId)  // Same conversation ID
            .WithSystemPrompt("You are a test assistant. Remember what users tell you.")
            .BuildChatAgentAsync();  // Should auto-load history
        
        var stats2 = agent2.GetStats();
        var response = await agent2.SendAsync("What's my name?");
        
        // Assert
        Assert.True(stats2.TotalMessages >= stats1.TotalMessages, 
            $"Should have loaded history: Session1={stats1.TotalMessages}, Session2={stats2.TotalMessages}");
        
        Assert.Contains("Alice", response.Text, StringComparison.OrdinalIgnoreCase);
        
        Output.WriteLine($"Session 2: {stats2.TotalMessages} messages (resumed from {stats1.TotalMessages})");
        Output.WriteLine($"Response: {response.Text}");
        
        await agent2.DisposeAsync();
    }
    
    [Fact]
    public async Task ConversationContinues_AfterRestart()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var dbContext = new TestDbContext();
        var historyStore = new EfCoreHistoryStore<TestDbContext>(dbContext, LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>());
        var conversationId = "continuation-test";
        
        // Session 1
        var agent1 = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation(conversationId)
            .WithSystemPrompt("You are a helpful math tutor.")
            .AddTool(new CalculatorTool())
            .BuildChatAgentAsync();
        
        await agent1.SendAsync("I'm working on a math problem.");
        await agent1.DisposeAsync();
        
        // Session 2 - Resume and continue
        var agent2 = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithObserver(Observer)
            .WithHistoryStore(historyStore)
            .ForConversation(conversationId)
            .WithSystemPrompt("You are a helpful math tutor.")
            .AddTool(new CalculatorTool())
            .BuildChatAgentAsync();
        
        // Act
        var response = await agent2.SendAsync("Can you help me calculate 15 * 8?");
        
        // Assert - Should maintain context and use calculator
        Assert.Contains("120", response.Text, StringComparison.OrdinalIgnoreCase);
        
        Output.WriteLine($"Resumed conversation and got: {response.Text}");
        
        await agent2.DisposeAsync();
    }
    
    [Fact]
    public async Task MultiTenancy_IsolatesConversations()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        var dbContext = new TestDbContext();
        
        var tenant1Store = new EfCoreHistoryStore<TestDbContext>(
            dbContext, 
            LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>(),
            tenantId: "tenant-1",
            userId: "user-a");
        
        var tenant2Store = new EfCoreHistoryStore<TestDbContext>(
            dbContext, 
            LoggerFactory.CreateLogger<EfCoreHistoryStore<TestDbContext>>(),
            tenantId: "tenant-2",
            userId: "user-b");
        
        // Act - Create conversations for different tenants
        var agent1 = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithHistoryStore(tenant1Store)
            .ForConversation("conv-1")
            .BuildChatAgentAsync();
        
        await agent1.SendAsync("Tenant 1 message");
        await agent1.DisposeAsync();
        
        var agent2 = await new AgentBuilder()
            .UseXAI(options =>
            {
                options.ApiKey = config.Providers.XAI.ApiKey;
                options.Model = config.Providers.XAI.Model;
            })
            .WithHistoryStore(tenant2Store)
            .ForConversation("conv-2")
            .BuildChatAgentAsync();
        
        await agent2.SendAsync("Tenant 2 message");
        await agent2.DisposeAsync();
        
        // Assert - List conversations should be isolated
        var tenant1Convs = await tenant1Store.ListConversationsAsync();
        var tenant2Convs = await tenant2Store.ListConversationsAsync();
        
        Assert.Single(tenant1Convs);
        Assert.Single(tenant2Convs);
        Assert.Contains("conv-1", tenant1Convs);
        Assert.Contains("conv-2", tenant2Convs);
        
        Output.WriteLine($"Tenant 1 conversations: {string.Join(", ", tenant1Convs)}");
        Output.WriteLine($"Tenant 2 conversations: {string.Join(", ", tenant2Convs)}");
    }
}

