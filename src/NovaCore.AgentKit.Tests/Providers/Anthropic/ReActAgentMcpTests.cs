using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.MCP;
using NovaCore.AgentKit.Providers.Anthropic;
using NovaCore.AgentKit.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Anthropic;

/// <summary>
/// ReActAgent with MCP Playwright integration tests
/// </summary>
public class ReActAgentMcpTests : ProviderTestBase
{
    public ReActAgentMcpTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task McpPlaywright_BrowserAutomation()
    {
        // Arrange
        var config = TestConfigHelper.GetConfig();
        
        var mcpConfig = new McpConfiguration
        {
            Command = "npx",
            Arguments = new List<string> { "-y", "@playwright/mcp" }
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
            .WithMcp(mcpConfig)
            .WithToolResultFiltering(cfg =>
            {
                cfg.KeepRecent = 1;  // Critical for browser - keep only last tool result
            })
            .WithReActConfig(cfg => cfg.MaxTurns = 15)
            .BuildReActAgentAsync();
        
        // Act
        var result = await agent.RunAsync("Go to example.com and tell me what the main heading says");
        
        // Assert
        Assert.True(result.Success, $"Task failed: {result.Error}");
        Assert.NotNull(result.FinalAnswer);
        Assert.NotEmpty(result.FinalAnswer);
        
        Output.WriteLine($"Success: {result.Success}");
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        Output.WriteLine($"Turns: {result.TurnsExecuted}");
        Output.WriteLine($"LLM calls: {result.TotalLlmCalls}");
        Output.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");
        
        await agent.DisposeAsync();
    }
}

