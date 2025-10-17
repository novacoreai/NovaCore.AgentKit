using Microsoft.Extensions.Logging;
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
            .WithLogger(Logger)
            .WithLoggerFactory(LoggerFactory)
            .WithLogging(cfg =>
            {
                cfg.LogUserInput = LogVerbosity.Full;
                cfg.LogAgentOutput = LogVerbosity.Full;
                cfg.LogToolCallRequests = LogVerbosity.Full;
                cfg.LogToolCallResponses = LogVerbosity.Full;
            })
            .WithMcpClientFactory(mcpFactory)
            .WithMcp(mcpConfig)
            .WithHistoryRetention(cfg =>
            {
                cfg.ToolResults.Strategy = ToolResultStrategy.KeepOne;
            })
            .WithReActConfig(cfg => cfg.MaxIterations = 5) // Reduce iterations for faster failure
            .BuildReActAgentAsync();
        
        var result = await agent.RunAsync("Go to example.com and tell me the main heading");
        
        // Log detailed information about what happened
        Logger.LogInformation("=== ReAct Execution Complete ===");
        Logger.LogInformation("Success: {Success}", result.Success);
        Logger.LogInformation("Total Iterations: {Count}", result.Iterations.Count);
        Logger.LogInformation("Total Tool Calls: {Count}", result.TotalToolCalls);
        Logger.LogInformation("Final Answer: {Answer}", result.FinalAnswer);
        
        // Log each iteration's details
        foreach (var iteration in result.Iterations)
        {
            Logger.LogInformation("--- Iteration {Number} ---", iteration.IterationNumber);
            Logger.LogInformation("Tool Calls: {Count}", iteration.ToolCallsExecuted);
            Logger.LogInformation("Thought: {Thought}", 
                iteration.Thought?.Substring(0, Math.Min(400, iteration.Thought?.Length ?? 0)) ?? "(no thought)");
        }
        
        Assert.True(result.Success, $"Agent failed. Iterations: {result.Iterations.Count}, Tool Calls: {result.TotalToolCalls}");
        Output.WriteLine($"Answer: {result.FinalAnswer}");
        
        await agent.DisposeAsync();
    }
}

