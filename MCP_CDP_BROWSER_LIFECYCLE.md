# MCP with CDP: Browser Lifecycle Management

## Overview

When using Playwright MCP with NovaCore.AgentKit, you can connect to an **existing browser instance** via Chrome DevTools Protocol (CDP). This allows your host application to maintain full control over the browser lifecycle while agents interact with it through MCP tools.

## Why Use CDP Mode?

- **Shared Browser State**: Multiple tools (agents, humans, computer vision) can interact with the same browser
- **Persistent Sessions**: Browser state (cookies, auth, localStorage) persists across agent connections
- **Workflow Orchestration**: Hand off control between different actors without restarting the browser
- **LLM-Optimized Tools**: Get all Playwright MCP's accessibility features while controlling the browser externally

## How It Works

```
Host Application
    ↓ (launches)
Chrome with --remote-debugging-port=9222
    ↓ (browser running)
Agent via MCP (--cdp-endpoint)
    ↓ (connects and controls)
Same Browser Instance
```

The host application owns the browser process. Agents connect via MCP, perform actions, then disconnect. The browser keeps running.

## Example 1: Basic CDP Connection

```csharp
// Host application launches Chrome with CDP enabled
var browserProcess = Process.Start(new ProcessStartInfo
{
    FileName = "chrome.exe",
    Arguments = "--remote-debugging-port=9222",
    UseShellExecute = false
});

await Task.Delay(2000); // Wait for browser startup

// Configure MCP to connect to existing browser
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> 
    { 
        "@playwright/mcp@latest",
        "--cdp-endpoint", "http://localhost:9222"
    }
};

var mcpFactory = new McpClientFactory(loggerFactory);

// Build agent that connects to the running browser
var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, AnthropicModels.ClaudeSonnet45)
    .WithMcpClientFactory(mcpFactory)
    .WithMcp(mcpConfig)
    .BuildChatAgentAsync();

// Agent interacts with the existing browser
await agent.SendAsync("Navigate to example.com and click the login button");

// Dispose agent (browser keeps running)
await agent.DisposeAsync();

// Browser is still running for other tools/humans to use
```

## Example 2: Workflow Orchestration

```csharp
public class BrowserWorkflowOrchestrator
{
    private Process _browserProcess;
    private readonly string _cdpEndpoint = "http://localhost:9222";
    private readonly ILoggerFactory _loggerFactory;
    
    public BrowserWorkflowOrchestrator(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }
    
    // Step 1: Start browser
    public async Task StartBrowserAsync()
    {
        _browserProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "chrome.exe",
            Arguments = "--remote-debugging-port=9222 --no-first-run",
            UseShellExecute = false
        });
        
        await Task.Delay(2000);
    }
    
    // Step 2: Human logs in and sets up state
    public async Task AllowHumanSetupAsync()
    {
        // Human interacts with browser directly
        // Logs in, navigates, configures settings
        Console.WriteLine("Browser ready for human interaction...");
        await Task.Delay(10000); // Human does their work
    }
    
    // Step 3: Agent takes over
    public async Task<ChatAgent> CreateAgentAsync(string apiKey)
    {
        var mcpConfig = new McpConfiguration
        {
            Command = "npx",
            Arguments = new List<string> 
            { 
                "@playwright/mcp@latest",
                "--cdp-endpoint", _cdpEndpoint
            }
        };
        
        var agent = await new AgentBuilder()
            .UseAnthropic(apiKey, AnthropicModels.ClaudeSonnet45)
            .WithMcpClientFactory(new McpClientFactory(_loggerFactory))
            .WithMcp(mcpConfig, new List<string> 
            {
                "playwright_navigate",
                "playwright_click",
                "playwright_fill",
                "playwright_screenshot"
            })
            .BuildChatAgentAsync();
            
        return agent;
    }
    
    // Step 4: Agent performs automated tasks
    public async Task RunAgentTasksAsync(ChatAgent agent)
    {
        await agent.SendAsync("Fill out the form with test data and submit");
        await agent.SendAsync("Take a screenshot of the confirmation page");
    }
    
    // Step 5: Hand back to human for verification
    public async Task HandBackToHumanAsync(ChatAgent agent)
    {
        await agent.DisposeAsync();
        Console.WriteLine("Agent disconnected. Human can now verify results...");
    }
    
    // Step 6: Cleanup
    public void Cleanup()
    {
        _browserProcess?.Kill();
        _browserProcess?.Dispose();
    }
}

// Usage
var orchestrator = new BrowserWorkflowOrchestrator(loggerFactory);

await orchestrator.StartBrowserAsync();
await orchestrator.AllowHumanSetupAsync();

var agent = await orchestrator.CreateAgentAsync(apiKey);
await orchestrator.RunAgentTasksAsync(agent);
await orchestrator.HandBackToHumanAsync(agent);

orchestrator.Cleanup();
```

## Example 3: Multi-Tool Handoff

```csharp
// Scenario: Agent → Computer Vision Tool → Agent

// 1. Start browser
var browser = Process.Start(new ProcessStartInfo
{
    FileName = "chrome.exe",
    Arguments = "--remote-debugging-port=9222"
});

await Task.Delay(2000);

// 2. Agent does initial work
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> { "@playwright/mcp@latest", "--cdp-endpoint", "http://localhost:9222" }
};

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithMcpClientFactory(new McpClientFactory(loggerFactory))
    .WithMcp(mcpConfig)
    .BuildChatAgentAsync();

await agent.SendAsync("Navigate to the dashboard");
await agent.DisposeAsync(); // Disconnect agent

// 3. Computer vision tool takes over (same browser)
var cuaTool = new ComputerVisionTool("http://localhost:9222");
var screenshot = await cuaTool.CaptureScreenAsync();
var analysis = await cuaTool.AnalyzeUIAsync(screenshot);

// 4. Agent reconnects based on CV analysis
agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithMcpClientFactory(new McpClientFactory(loggerFactory))
    .WithMcp(mcpConfig)
    .BuildChatAgentAsync();

await agent.SendAsync($"Based on the analysis: {analysis}, click the appropriate button");
await agent.DisposeAsync();

// Browser still running for next tool/human
```

## Example 4: Remote Browser (Browserbase, etc.)

```csharp
// Connect to a remote browser service
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> 
    { 
        "@playwright/mcp@latest",
        "--cdp-endpoint", "wss://browserbase.com/v1/sessions/abc123",
        "--cdp-header", $"Authorization: Bearer {browserbas eApiKey}"
    }
};

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithMcpClientFactory(new McpClientFactory(loggerFactory))
    .WithMcp(mcpConfig)
    .BuildChatAgentAsync();

// Agent controls remote browser
await agent.SendAsync("Navigate to the application and perform the workflow");
```

## Configuration Options

### Basic CDP Connection
```csharp
Arguments = new List<string> 
{ 
    "@playwright/mcp@latest",
    "--cdp-endpoint", "http://localhost:9222"
}
```

### With Authentication Headers
```csharp
Arguments = new List<string> 
{ 
    "@playwright/mcp@latest",
    "--cdp-endpoint", "http://localhost:9222",
    "--cdp-header", "Authorization: Bearer token123"
}
```

### With Timeouts
```csharp
Arguments = new List<string> 
{ 
    "@playwright/mcp@latest",
    "--cdp-endpoint", "http://localhost:9222",
    "--timeout-action", "10000",      // 10s action timeout
    "--timeout-navigation", "30000"   // 30s navigation timeout
}
```

### With Tool Filtering
```csharp
var allowedTools = new List<string> 
{ 
    "playwright_navigate",
    "playwright_click",
    "playwright_screenshot"
};

.WithMcp(mcpConfig, allowedTools)
```

## Key Points

1. **Host Application Owns Browser**: Your application launches and manages the Chrome/Chromium process
2. **Library is Agnostic**: NovaCore.AgentKit simply passes CDP configuration to the MCP server
3. **Agents Connect/Disconnect**: Agents can be created and disposed without affecting the browser
4. **State Persists**: All browser state (cookies, auth, localStorage) remains intact between agent sessions
5. **Multiple Tools**: Different tools can take turns controlling the same browser instance

## Launching Chrome with CDP

### Windows
```bash
chrome.exe --remote-debugging-port=9222
```

### Linux
```bash
google-chrome --remote-debugging-port=9222
```

### macOS
```bash
/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome --remote-debugging-port=9222
```

### Programmatically (C#)
```csharp
var process = Process.Start(new ProcessStartInfo
{
    FileName = "chrome.exe",  // or full path
    Arguments = "--remote-debugging-port=9222 --no-first-run --no-default-browser-check",
    UseShellExecute = false,
    CreateNoWindow = false  // Set true for headless
});
```

## Troubleshooting

### Connection Refused
- Ensure Chrome is running with `--remote-debugging-port=9222`
- Check port is not already in use
- Wait a few seconds after launching Chrome before connecting

### State Not Persisting
- Verify you're disposing the agent, not killing the browser process
- Check that the same CDP endpoint is used across connections

### Tools Not Working
- Ensure Playwright MCP version supports CDP mode (latest recommended)
- Verify the CDP endpoint is accessible from where the MCP server runs

## References

- [Playwright MCP Official Repo](https://github.com/microsoft/playwright-mcp)
- [Chrome DevTools Protocol](https://chromedevtools.github.io/devtools-protocol/)
- [Playwright connectOverCDP Documentation](https://playwright.dev/docs/api/class-browsertype#browser-type-connect-over-cdp)
