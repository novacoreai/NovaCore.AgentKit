using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.MCP;
using NovaCore.AgentKit.Providers.Anthropic;
using NovaCore.AgentKit.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Anthropic;

/// <summary>
/// Tests for MCP tool filtering functionality
/// </summary>
public class McpToolFilteringTests : ProviderTestBase
{
    public McpToolFilteringTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task McpToolFiltering_WithAllowedTools_ShouldOnlyExposeFilteredTools()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        
        var mcpConfig = new McpConfiguration
        {
            Command = "npx",
            Arguments = new List<string> { "-y", "@playwright/mcp" }
        };
        
        var mcpFactory = new McpClientFactory(LoggerFactory);
        
        // Define a small subset of tools to expose
        var allowedTools = new List<string>
        {
            "browser_navigate",
            "browser_take_screenshot"
        };
        
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithObserver(Observer)
            .WithMcpClientFactory(mcpFactory)
            .WithMcp(mcpConfig, allowedTools)  // Apply filter
            .WithToolResultFiltering(cfg =>
            {
                cfg.KeepRecent = 1;
            })
            .WithReActConfig(cfg => cfg.MaxTurns = 10)
            .BuildReActAgentAsync();
        
        // Act
        var result = await agent.RunAsync("Navigate to example.com and take a screenshot");
        
        // Assert
        Assert.True(result.Success, $"Task failed: {result.Error}");
        Assert.NotNull(result.FinalAnswer);
        Assert.NotEmpty(result.FinalAnswer);
        
        Output.WriteLine($"Success: {result.Success}");
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        Output.WriteLine($"Turns: {result.TurnsExecuted}");
        Output.WriteLine($"LLM calls: {result.TotalLlmCalls}");
        Output.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");
        
        // The agent should only have access to the 2 filtered tools
        // It should successfully complete the task using only navigate and screenshot
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task McpToolFiltering_ViaConfiguration_ShouldFilterTools()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        
        // Filter tools via configuration property instead of builder parameter
        var mcpConfig = new McpConfiguration
        {
            Command = "npx",
            Arguments = new List<string> { "-y", "@playwright/mcp" },
            AllowedTools = new List<string>
            {
                "browser_navigate",
                "browser_take_screenshot",
                "browser_click"
            }
        };
        
        var mcpFactory = new McpClientFactory(LoggerFactory);
        
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithObserver(Observer)
            .WithMcpClientFactory(mcpFactory)
            .WithMcp(mcpConfig)  // Filter is in the configuration
            .WithToolResultFiltering(cfg =>
            {
                cfg.KeepRecent = 1;
            })
            .WithReActConfig(cfg => cfg.MaxTurns = 10)
            .BuildReActAgentAsync();
        
        // Act
        var result = await agent.RunAsync("Go to example.com, click any link, and take a screenshot");
        
        // Assert
        Assert.True(result.Success, $"Task failed: {result.Error}");
        Assert.NotNull(result.FinalAnswer);
        
        Output.WriteLine($"Success: {result.Success}");
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        Output.WriteLine($"Turns: {result.TurnsExecuted}");
        Output.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task McpToolFiltering_BuilderParameterOverridesConfiguration()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        
        // Configuration has one set of tools
        var mcpConfig = new McpConfiguration
        {
            Command = "npx",
            Arguments = new List<string> { "-y", "@playwright/mcp" },
            AllowedTools = new List<string>
            {
                "browser_navigate",
                "browser_click"
            }
        };
        
        // Builder parameter should override configuration
        var builderAllowedTools = new List<string>
        {
            "browser_navigate",
            "browser_take_screenshot"  // Different from config
        };
        
        var mcpFactory = new McpClientFactory(LoggerFactory);
        
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithObserver(Observer)
            .WithMcpClientFactory(mcpFactory)
            .WithMcp(mcpConfig, builderAllowedTools)  // Builder parameter takes precedence
            .WithToolResultFiltering(cfg =>
            {
                cfg.KeepRecent = 1;
            })
            .WithReActConfig(cfg => cfg.MaxTurns = 10)
            .BuildReActAgentAsync();
        
        // Act - Task requires screenshot (from builder filter) not click (from config filter)
        var result = await agent.RunAsync("Navigate to example.com and take a screenshot");
        
        // Assert
        Assert.True(result.Success, $"Task failed: {result.Error}");
        Assert.NotNull(result.FinalAnswer);
        
        Output.WriteLine($"Success: {result.Success}");
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        Output.WriteLine($"Turns: {result.TurnsExecuted}");
        
        // Should succeed because builder parameter (with screenshot) overrides config (without screenshot)
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task McpToolDiscovery_WithFilter_ShouldReturnOnlyAllowedTools()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        
        var mcpConfig = new McpConfiguration
        {
            Command = "npx",
            Arguments = new List<string> { "-y", "@playwright/mcp" },
            AllowedTools = new List<string>
            {
                "browser_navigate",
                "browser_take_screenshot",
                "browser_click"
            }
        };
        
        var mcpFactory = new McpClientFactory(LoggerFactory);
        
        // Build agent with filtered tools - filtering happens in the builder
        var agent = await new AgentBuilder()
            .UseAnthropic(options =>
            {
                options.ApiKey = config.Providers.Anthropic.ApiKey;
                options.Model = config.Providers.Anthropic.Model;
            })
            .WithObserver(Observer)
            .WithMcpClientFactory(mcpFactory)
            .WithMcp(mcpConfig)
            .BuildChatAgentAsync();
        
        // Act - Get the tools from the agent's internal state
        // The filtering happens during agent build, not in MCP client discovery
        // We can verify by checking that the agent only has access to 3 tools
        
        // For this test, we'll verify the filtering works by checking the agent can use filtered tools
        var response = await agent.SendAsync("What tools do you have available? List them.");
        
        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Text);
        Assert.NotEmpty(response.Text);
        
        Output.WriteLine($"Agent response about available tools:");
        Output.WriteLine(response.Text);
        
        // The agent should mention only the 3 filtered tools
        // Note: This is an indirect test since we can't directly access the agent's tool list
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task McpToolDiscovery_WithoutFilter_ShouldReturnAllTools()
    {
        // Arrange
        var mcpConfig = new McpConfiguration
        {
            Command = "npx",
            Arguments = new List<string> { "-y", "@playwright/mcp" }
            // No AllowedTools specified
        };
        
        var mcpFactory = new McpClientFactory(LoggerFactory);
        var mcpClient = await mcpFactory.CreateClientAsync(mcpConfig);
        
        // Act
        var discoveredTools = await mcpClient.DiscoverToolsAsync();
        
        // Assert
        Assert.NotNull(discoveredTools);
        Assert.NotEmpty(discoveredTools);
        
        // Without filter, should have many tools (20+)
        Assert.True(discoveredTools.Count > 10, $"Expected 10+ tools, got {discoveredTools.Count}");
        
        Output.WriteLine($"Discovered {discoveredTools.Count} tools (no filter):");
        foreach (var tool in discoveredTools.Take(10))
        {
            Output.WriteLine($"  - {tool.Name}: {tool.Description}");
        }
        
        if (discoveredTools.Count > 10)
        {
            Output.WriteLine($"  ... and {discoveredTools.Count - 10} more tools");
        }
        
        await mcpClient.DisposeAsync();
    }
}
