using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.MCP;
using NovaCore.AgentKit.Providers.Google;
using NovaCore.AgentKit.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Google;

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
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = config.Providers.Google.Model;
            })
            .WithLogger(Logger)
            .WithLoggerFactory(LoggerFactory)
            .WithMcpClientFactory(mcpFactory)
            .WithMcp(mcpConfig)
            .WithHistoryRetention(cfg =>
            {
                cfg.ToolResults.Strategy = ToolResultStrategy.KeepOne;
                cfg.MaxMessagesToSend = 20;
            })
            .WithReActConfig(cfg => cfg.MaxIterations = 15)
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("Go to example.com and tell me the main heading");
        
        Assert.True(result.Success, $"Failed: {result.Error}");
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        Output.WriteLine($"Iterations: {result.Iterations.Count}, Tools: {result.TotalToolCalls}");
        
        await agent.DisposeAsync();
    }
}

