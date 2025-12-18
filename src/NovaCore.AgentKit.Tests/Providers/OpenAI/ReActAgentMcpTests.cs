using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.MCP;
using NovaCore.AgentKit.Providers.OpenAI;
using NovaCore.AgentKit.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.OpenAI;

public class ReActAgentMcpTests : ProviderTestBase
{
    public ReActAgentMcpTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task McpPlaywright_BrowserAutomation()
    {
        var config = TestConfigHelper.GetConfig();
        var mcpConfig = new McpConfiguration
        {
            Command = "npx",
            Arguments = new List<string> { "-y", "@playwright/mcp" }
        };
        
        var mcpFactory = new McpClientFactory(LoggerFactory);
        
        var agent = await new AgentBuilder()
            .UseOpenAI(options =>
            {
                options.ApiKey = config.Providers.OpenAI.ApiKey;
                options.Model = config.Providers.OpenAI.Model;
            })
            .WithObserver(Observer)
            .WithMcpClientFactory(mcpFactory)
            .WithMcp(mcpConfig)
            .WithToolResultFiltering(cfg =>
            {
                cfg.KeepRecent = 1;  // Keep only last tool result (browser agent pattern)
            })
            .WithReActConfig(cfg => cfg.MaxTurns = 15)
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("Go to example.com and tell me the main heading");
        
        Assert.True(result.Success);
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        
        await agent.DisposeAsync();
    }
}

