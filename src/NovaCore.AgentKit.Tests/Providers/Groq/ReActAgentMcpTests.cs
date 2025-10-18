using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.MCP;
using NovaCore.AgentKit.Providers.Groq;
using NovaCore.AgentKit.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Groq;

public class ReActAgentMcpTests : ProviderTestBase
{
    public ReActAgentMcpTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task McpPlaywright_BrowserAutomation()
    {
        // NOTE: Using llama-3.3-70b-versatile instead of gpt-oss-120b
        // GPT OSS 120 has known issues with tool calling - it ignores tools
        // See: https://community.groq.com/t/gpt-oss-120b-ignoring-tools/385
        var config = TestConfigHelper.GetConfig();
        var mcpConfig = new McpConfiguration
        {
            Command = "npx",
            Arguments = new List<string> { "-y", "@playwright/mcp" }
        };
        
        var mcpFactory = new McpClientFactory(LoggerFactory);
        
        var agent = await new AgentBuilder()
            .UseGroq(options =>
            {
                options.ApiKey = config.Providers.Groq.ApiKey;
                options.Model = config.Providers.Groq.Model;
            })
            .WithObserver(Observer)
            .WithMcpClientFactory(mcpFactory)
            .WithMcp(mcpConfig)
            .WithToolResultFiltering(cfg =>
            {
                cfg.KeepRecent = 1;  // Keep only last tool result (browser agent pattern)
            })
            .WithReActConfig(cfg => cfg.MaxTurns = 5) // Reduce iterations for faster failure
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("Go to example.com and tell me the main heading");
        
        // Output results
        Output.WriteLine($"Success: {result.Success}");
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        Output.WriteLine($"Turns: {result.TurnsExecuted}");
        Output.WriteLine($"LLM calls: {result.TotalLlmCalls}");
        
        Assert.True(result.Success, $"Agent failed. Turns: {result.TurnsExecuted}, LLM calls: {result.TotalLlmCalls}");
        
        await agent.DisposeAsync();
    }
}

