using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.History;
using Xunit;

namespace NovaCore.AgentKit.Tests.Core;

/// <summary>
/// Tests for tool result filtering with placeholder approach
/// </summary>
public class ToolResultFilteringTests
{
    private readonly ILogger<ToolResultFilteringTests> _logger;
    
    public ToolResultFilteringTests()
    {
        // Use null logger for tests (no output needed)
        _logger = NullLogger<ToolResultFilteringTests>.Instance;
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
        
        var config = new ToolResultConfig
        {
            KeepRecent = 4  // Keep last 4 tool results with full content
        };
        
        var selector = new SmartHistorySelector();
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        AssertValidConversationStructure(filtered);
        
        // All 10 tool results are present, but only 4 have full content
        var toolResults = filtered.Where(m => m.Role == ChatRole.Tool).ToList();
        Assert.Equal(10, toolResults.Count);  // All tool results still present
        
        var fullContentResults = toolResults.Count(m => m.Text != "[Omitted]");
        var placeholderResults = toolResults.Count(m => m.Text == "[Omitted]");
        
        Assert.Equal(4, fullContentResults);  // Only 4 have full content
        Assert.Equal(6, placeholderResults);  // 6 are placeholders
    }
    
    /// <summary>
    /// Test KeepRecent = 1 (useful for browser agents)
    /// </summary>
    [Fact]
    public void FilterToolResults_KeepOne_OnlyLastHasFullContent()
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
        
        var config = new ToolResultConfig
        {
            KeepRecent = 1  // Keep only last tool result
        };
        
        var selector = new SmartHistorySelector();
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        var toolResults = filtered.Where(m => m.Role == ChatRole.Tool).ToList();
        
        // All tool results are present, but filtered ones are replaced with "[Omitted]"
        Assert.Equal(3, toolResults.Count);
        Assert.Equal("[Omitted]", toolResults[0].Text);  // Result 1 replaced
        Assert.Equal("[Omitted]", toolResults[1].Text);  // Result 2 replaced
        Assert.Equal("Result 3", toolResults[2].Text);   // Result 3 kept
        
        // All Assistant messages preserved
        var assistants = filtered.Where(m => m.Role == ChatRole.Assistant).ToList();
        Assert.Equal(3, assistants.Count);
        
        AssertValidConversationStructure(filtered);
    }
    
    /// <summary>
    /// Test KeepRecent = 0 (unlimited, no filtering)
    /// </summary>
    [Fact]
    public void FilterToolResults_KeepRecentZero_KeepsAllToolResults()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Do task"),
            new ChatMessage(ChatRole.Assistant, "Step 1", new List<ToolCall> { new ToolCall { Id = "tc_1", FunctionName = "tool", Arguments = "{}" } }),
            new ChatMessage(ChatRole.Tool, "Result 1", "tc_1"),
            new ChatMessage(ChatRole.Assistant, "Done"),
        };
        
        var config = new ToolResultConfig
        {
            KeepRecent = 0  // No filtering
        };
        
        var selector = new SmartHistorySelector();
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        var toolResults = filtered.Where(m => m.Role == ChatRole.Tool).ToList();
        Assert.Single(toolResults);
        Assert.Equal("Result 1", toolResults[0].Text);  // Full content preserved
        
        Assert.Equal(messages.Count, filtered.Count);
        AssertValidConversationStructure(filtered);
    }
    
    /// <summary>
    /// Test that placeholder approach preserves all messages and structure
    /// </summary>
    [Fact]
    public void FilterMessages_PreservesAllMessagesWithPlaceholders()
    {
        // Arrange
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
        
        var config = new ToolResultConfig
        {
            KeepRecent = 1  // Only last has full content
        };
        
        var selector = new SmartHistorySelector();
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert
        AssertValidConversationStructure(filtered);
        
        // All messages are preserved
        Assert.Equal(messages.Count, filtered.Count);
        
        // All tool results are present
        var toolResults = filtered.Where(m => m.Role == ChatRole.Tool).ToList();
        Assert.Equal(3, toolResults.Count);
        
        // First 2 are placeholders, last one has full content
        Assert.Equal("[Omitted]", toolResults[0].Text);
        Assert.Equal("[Omitted]", toolResults[1].Text);
        Assert.Equal("Search result 3", toolResults[2].Text);
        
        // CRITICAL: Verify no orphaned Tool results - all have corresponding Assistant tool calls
        var assistantToolCallIds = filtered
            .Where(m => m.Role == ChatRole.Assistant && m.ToolCalls != null)
            .SelectMany(m => m.ToolCalls!)
            .Select(tc => tc.Id)
            .ToHashSet();
        
        foreach (var toolResult in toolResults)
        {
            Assert.True(
                assistantToolCallIds.Contains(toolResult.ToolCallId!),
                $"Tool result {toolResult.ToolCallId} has no corresponding Assistant tool call!");
        }
        
        // All 3 Assistants are preserved
        var assistants = filtered.Where(m => m.Role == ChatRole.Assistant).ToList();
        Assert.Equal(3, assistants.Count);
    }
    
    /// <summary>
    /// Test that consecutive Assistants WITH Tool results between them are VALID.
    /// This is the pattern used in multi-round tool calling within a single turn.
    /// </summary>
    [Fact]
    public void FilterMessages_AllowsConsecutiveAssistants_WhenToolResultsBetween()
    {
        // Arrange: Simulate within-turn tool calling
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
        
        var config = new ToolResultConfig
        {
            KeepRecent = 0  // No filtering
        };
        
        var selector = new SmartHistorySelector();
        
        // Act
        var filtered = selector.SelectMessagesForContext(messages, config);
        
        // Assert - should NOT need repair because this is valid!
        Assert.Equal(messages.Count, filtered.Count);
        AssertValidConversationStructure(filtered);
    }
    
    /// <summary>
    /// Test config summary string
    /// </summary>
    [Fact]
    public void ToolResultConfig_GetSummary_ReturnsReadableString()
    {
        // Test unlimited
        var unlimitedConfig = new ToolResultConfig { KeepRecent = 0 };
        Assert.Equal("unlimited (no filtering)", unlimitedConfig.GetSummary());
        
        // Test with limit
        var limitedConfig = new ToolResultConfig { KeepRecent = 5 };
        Assert.Contains("5", limitedConfig.GetSummary());
        Assert.Contains("placeholder", limitedConfig.GetSummary());
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

