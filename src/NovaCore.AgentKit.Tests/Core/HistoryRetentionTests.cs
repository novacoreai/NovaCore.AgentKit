using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.History;
using Xunit;

namespace NovaCore.AgentKit.Tests.Core;

/// <summary>
/// Tests for history retention and filtering edge cases
/// </summary>
public class HistoryRetentionTests
{
    private readonly ILogger<HistoryRetentionTests> _logger;
    
    public HistoryRetentionTests()
    {
        // Use null logger for tests (no output needed)
        _logger = NullLogger<HistoryRetentionTests>.Instance;
    }
    
    /// <summary>
    /// Test that filtering multiple tool rounds maintains conversation structure
    /// </summary>
    [Fact]
    public void FilterToolResults_WithMultipleRounds_MaintainsConversationStructure()
    {
        // Arrange: Simulate ReAct agent with 10 tool rounds
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "You are a helpful assistant"),
            new ChatMessage(ChatRole.User, "Do task"),
        };
        
        // Add 10 tool rounds (each round = Assistant + Tool)
        for (int i = 1; i <= 10; i++)
        {
            messages.Add(new ChatMessage(
                ChatRole.Assistant, 
                $"Executing step {i}",
                new List<ToolCall> { new ToolCall { Id = $"tc_{i}", FunctionName = "browser_click", Arguments = "{}" } }
            ));
            messages.Add(new ChatMessage(ChatRole.Tool, $"Result {i}", $"tc_{i}"));
        }
        
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 20,
            KeepRecentMessagesIntact = 10,
            ToolResults = new ToolResultRetentionConfig
            {
                Strategy = ToolResultStrategy.KeepRecent,
                MaxToolResults = 4
            }
        };
        
        var selector = new SmartHistorySelector(_logger);
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        _logger.LogInformation("Original messages: {Count}, Filtered: {Count}", messages.Count, filtered.Count);
        
        // Should have valid conversation structure (no consecutive Assistant messages)
        AssertValidConversationStructure(filtered);
        
        // Should have no more than 4 tool results
        var toolResults = filtered.Count(m => m.Role == ChatRole.Tool);
        Assert.True(toolResults <= 4, $"Expected <= 4 tool results, got {toolResults}");
    }
    
    /// <summary>
    /// Test KeepRecentMessagesIntact >= MaxMessagesToSend edge case
    /// </summary>
    [Fact]
    public void FilterMessages_WhenKeepRecentEqualsMax_KeepsOnlyRecent()
    {
        // Arrange
        var messages = new List<ChatMessage>();
        for (int i = 0; i < 30; i++)
        {
            messages.Add(new ChatMessage(ChatRole.User, $"Message {i}"));
            messages.Add(new ChatMessage(ChatRole.Assistant, $"Response {i}"));
        }
        
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 20,
            KeepRecentMessagesIntact = 20  // Equal to max!
        };
        
        var selector = new SmartHistorySelector(_logger);
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        Assert.Equal(20, filtered.Count);
        AssertValidConversationStructure(filtered);
        
        // Should keep the most recent 20
        Assert.Equal("Message 20", filtered[0].Text);
    }
    
    /// <summary>
    /// Test that MaxToolResults = 1 works correctly
    /// </summary>
    [Fact]
    public void FilterToolResults_KeepOne_KeepsOnlyLastToolResult()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Do task"),
            new ChatMessage(ChatRole.Assistant, "Step 1", new List<ToolCall> { new ToolCall { Id = "tc_1", FunctionName = "tool", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Result 1", "tc_1"),
            new ChatMessage(ChatRole.Assistant, "Step 2", new List<ToolCall> { new ToolCall { Id = "tc_2", FunctionName = "tool", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Result 2", "tc_2"),
            new ChatMessage(ChatRole.Assistant, "Step 3", new List<ToolCall> { new ToolCall { Id = "tc_3", FunctionName = "tool", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Result 3", "tc_3"),
        };
        
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 0,  // Unlimited
            ToolResults = new ToolResultRetentionConfig
            {
                Strategy = ToolResultStrategy.KeepOne,
                MaxToolResults = 1
            }
        };
        
        var selector = new SmartHistorySelector(_logger);
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        var toolResults = filtered.Where(m => m.Role == ChatRole.Tool).ToList();
        Assert.Single(toolResults);
        Assert.Equal("Result 3", toolResults[0].Text);
        
        AssertValidConversationStructure(filtered);
    }
    
    /// <summary>
    /// Test DropAll strategy
    /// </summary>
    [Fact]
    public void FilterToolResults_DropAll_RemovesAllToolResults()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Do task"),
            new ChatMessage(ChatRole.Assistant, "Step 1", new List<ToolCall> { new ToolCall { Id = "tc_1", FunctionName = "tool", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Result 1", "tc_1"),
            new ChatMessage(ChatRole.Assistant, "Done"),
        };
        
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 0,
            ToolResults = new ToolResultRetentionConfig
            {
                Strategy = ToolResultStrategy.DropAll
            }
        };
        
        var selector = new SmartHistorySelector(_logger);
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        Assert.DoesNotContain(filtered, m => m.Role == ChatRole.Tool);
        AssertValidConversationStructure(filtered);
    }
    
    /// <summary>
    /// Test that conversation repair works when filtering creates invalid structure
    /// </summary>
    [Fact]
    public void FilterMessages_RepairsInvalidConversationStructure()
    {
        // Arrange: Create a scenario that would produce consecutive Assistants after filtering
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Task"),
            new ChatMessage(ChatRole.Assistant, "Step 1", new List<ToolCall> { new ToolCall { Id = "tc_1", FunctionName = "tool", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Result 1", "tc_1"),
            new ChatMessage(ChatRole.Assistant, "Step 2", new List<ToolCall> { new ToolCall { Id = "tc_2", FunctionName = "tool", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Result 2", "tc_2"),
            new ChatMessage(ChatRole.Assistant, "Step 3"),  // No tool calls
        };
        
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 0,
            ToolResults = new ToolResultRetentionConfig
            {
                Strategy = ToolResultStrategy.KeepRecent,
                MaxToolResults = 1  // Will drop Result 1, which orphans Assistant Step 1
            }
        };
        
        var selector = new SmartHistorySelector(_logger);
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        AssertValidConversationStructure(filtered);
    }
    
    /// <summary>
    /// CRITICAL TEST: When tool result filtering orphans an Assistant, repair should drop that Assistant AND prevent orphaned Tool results.
    /// This prevents "unexpected tool_use_id" errors from Anthropic API.
    /// </summary>
    [Fact]
    public void FilterMessages_DropsOrphanedToolResults_WhenFilteringCreatesInvalidStructure()
    {
        // Arrange: Simulate tool result filtering that creates consecutive Assistants WITHOUT Tool results between them
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Find visa options"),
            
            // First tool call
            new ChatMessage(ChatRole.Assistant, "Let me search", 
                new List<ToolCall> { new ToolCall { Id = "tc_1", FunctionName = "search", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Search result 1", "tc_1"),
            
            // Second tool call
            new ChatMessage(ChatRole.Assistant, "Let me search more", 
                new List<ToolCall> { new ToolCall { Id = "tc_2", FunctionName = "search", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Search result 2", "tc_2"),
            
            // Third tool call
            new ChatMessage(ChatRole.Assistant, "One more search", 
                new List<ToolCall> { new ToolCall { Id = "tc_3", FunctionName = "search", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Search result 3", "tc_3"),
        };
        
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 50,
            KeepRecentMessagesIntact = 5,
            ToolResults = new ToolResultRetentionConfig
            {
                Strategy = ToolResultStrategy.KeepRecent,
                MaxToolResults = 1  // Aggressive filtering - will drop Result 1 and 2, orphaning their Assistants
            }
        };
        
        var selector = new SmartHistorySelector(_logger);
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        AssertValidConversationStructure(filtered);
        
        // CRITICAL: Verify no orphaned Tool results
        var toolResults = filtered.Where(m => m.Role == ChatRole.Tool).ToList();
        var assistantToolCallIds = filtered
            .Where(m => m.Role == ChatRole.Assistant && m.ToolCalls != null)
            .SelectMany(m => m.ToolCalls!)
            .Select(tc => tc.Id)
            .ToHashSet();
        
        foreach (var toolResult in toolResults)
        {
            Assert.True(
                assistantToolCallIds.Contains(toolResult.ToolCallId!),
                $"Tool result {toolResult.ToolCallId} has no corresponding Assistant tool call. This would cause Anthropic API error!"
            );
        }
        
        // Should have dropped orphaned Assistants and their Tool results
        Assert.True(filtered.Count < messages.Count, "Should have dropped some messages during filtering/repair");
        
        // Should only have 1 tool result (as configured)
        Assert.Single(toolResults);
    }
    
    /// <summary>
    /// Test that consecutive Assistants WITH Tool results between them are VALID.
    /// This is the pattern used in multi-round tool calling within a single turn.
    /// </summary>
    [Fact]
    public void FilterMessages_AllowsConsecutiveAssistants_WhenToolResultsBetween()
    {
        // Arrange: Simulate within-turn tool calling (what happens in Agent.cs loop)
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Find visa options"),
            
            // First tool round
            new ChatMessage(ChatRole.Assistant, "Let me search", 
                new List<ToolCall> { new ToolCall { Id = "tc_1", FunctionName = "search", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Result 1", "tc_1"),
            
            // Second tool round (SAME TURN - this is VALID!)
            new ChatMessage(ChatRole.Assistant, "Let me search more", 
                new List<ToolCall> { new ToolCall { Id = "tc_2", FunctionName = "search", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Result 2", "tc_2"),
        };
        
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 50,
            KeepRecentMessagesIntact = 5
        };
        
        var selector = new SmartHistorySelector(_logger);
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert - should NOT trigger repair because this is valid!
        Assert.Equal(messages.Count, filtered.Count); // No messages dropped
        AssertValidConversationStructure(filtered);
    }
    
    /// <summary>
    /// Test configuration validation
    /// </summary>
    [Fact]
    public void HistoryRetentionConfig_Validate_DetectsIssues()
    {
        // Arrange
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 20,
            KeepRecentMessagesIntact = 20  // Bad: equals max
        };
        
        // Act
        var issues = config.Validate();
        
        // Assert
        Assert.NotEmpty(issues);
        Assert.Contains("should be less than MaxMessagesToSend", issues[0]);
    }
    
    /// <summary>
    /// Test configuration validation for KeepRecent > 50% of Max
    /// </summary>
    [Fact]
    public void HistoryRetentionConfig_Validate_WarnsWhenKeepRecentTooHigh()
    {
        // Arrange
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 20,
            KeepRecentMessagesIntact = 15  // 75% of max
        };
        
        // Act
        var issues = config.Validate();
        
        // Assert
        Assert.NotEmpty(issues);
        Assert.Contains("more than 50%", issues[0]);
    }
    
    /// <summary>
    /// Test configuration summary
    /// </summary>
    [Fact]
    public void HistoryRetentionConfig_GetSummary_ReturnsReadableString()
    {
        // Arrange
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 50,
            KeepRecentMessagesIntact = 10,
            ToolResults = new ToolResultRetentionConfig
            {
                Strategy = ToolResultStrategy.KeepRecent,
                MaxToolResults = 5
            }
        };
        
        // Act
        var summary = config.GetSummary();
        
        // Assert
        Assert.Contains("50", summary);
        Assert.Contains("10", summary);
        Assert.Contains("5", summary);
    }
    
    /// <summary>
    /// Test with very aggressive filtering (MaxMessages = 5, MaxToolResults = 1)
    /// </summary>
    [Fact]
    public void FilterMessages_AggressiveFiltering_MaintainsValidity()
    {
        // Arrange: Large conversation
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "System"),
            new ChatMessage(ChatRole.User, "Task"),
        };
        
        for (int i = 1; i <= 20; i++)
        {
            messages.Add(new ChatMessage(
                ChatRole.Assistant, 
                $"Step {i}",
                new List<ToolCall> { new ToolCall { Id = $"tc_{i}", FunctionName = "tool", Arguments = "{}" } }
            ));
            messages.Add(new ChatMessage(ChatRole.Tool, $"Result {i}", $"tc_{i}"));
        }
        
        var config = new HistoryRetentionConfig
        {
            MaxMessagesToSend = 5,
            KeepRecentMessagesIntact = 2,
            ToolResults = new ToolResultRetentionConfig
            {
                Strategy = ToolResultStrategy.KeepOne,
                MaxToolResults = 1
            }
        };
        
        var selector = new SmartHistorySelector(_logger);
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        _logger.LogInformation("Aggressive filtering: {Original} → {Filtered}", messages.Count, filtered.Count);
        
        // Should be very short
        Assert.True(filtered.Count <= 10, $"Expected <= 10 messages after aggressive filtering, got {filtered.Count}");
        
        // Should still be valid
        AssertValidConversationStructure(filtered);
        
        // Should have at most 1 tool result
        var toolResults = filtered.Count(m => m.Role == ChatRole.Tool);
        Assert.True(toolResults <= 1, $"Expected <= 1 tool result, got {toolResults}");
    }
    
    /// <summary>
    /// Helper to assert conversation structure is valid.
    /// Allows consecutive Assistants if Tool results are between them (multi-round tool calling).
    /// </summary>
    private void AssertValidConversationStructure(List<ChatMessage> messages)
    {
        if (messages.Count == 0) return;
        
        // Check alternation, allowing Assistant->Tool->Assistant pattern
        for (int i = 1; i < messages.Count; i++)
        {
            var prev = messages[i - 1];
            var curr = messages[i];
            
            // Skip Tool messages for this check
            if (curr.Role == ChatRole.Tool || prev.Role == ChatRole.Tool)
            {
                continue;
            }
            
            // Skip System messages
            if (curr.Role == ChatRole.System || prev.Role == ChatRole.System)
            {
                continue;
            }
            
            // Check for invalid consecutive Assistants
            if (prev.Role == ChatRole.Assistant && curr.Role == ChatRole.Assistant)
            {
                // Check if there are Tool messages between these two Assistants
                bool hasToolsBetween = false;
                for (int j = i - 1; j >= 0 && messages[j].Role != ChatRole.User; j--)
                {
                    if (messages[j].Role == ChatRole.Tool)
                    {
                        hasToolsBetween = true;
                        break;
                    }
                }
                
                // Consecutive Assistants are OK if there are Tool results between them
                if (!hasToolsBetween || prev.ToolCalls == null || !prev.ToolCalls.Any())
                {
                    Assert.Fail($"Invalid conversation: Two consecutive Assistant messages at positions {i-1} and {i} without Tool results between them");
                }
            }
            
            // Check for consecutive User messages
            if (prev.Role == ChatRole.User && curr.Role == ChatRole.User)
            {
                Assert.Fail($"Invalid conversation: Two consecutive User messages at positions {i-1} and {i}");
            }
        }
    }
}

