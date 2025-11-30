# MCP Tool Filtering Example

## Overview

The `.WithMcp()` method now supports optional tool filtering to control which tools from an MCP server are exposed to your agent. This helps reduce context size, token usage, and potential confusion when an MCP server provides many tools but you only need a subset.

## Usage

### Method 1: Filter via Builder Parameter

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> { "-y", "@playwright/mcp" }
};

var mcpFactory = new McpClientFactory(loggerFactory);

// Only expose specific Playwright tools
var allowedTools = new List<string> 
{ 
    "playwright_navigate",
    "playwright_screenshot",
    "playwright_click"
};

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithMcpClientFactory(mcpFactory)
    .WithMcp(mcpConfig, allowedTools)  // Pass filter as second parameter
    .BuildChatAgentAsync();
```

### Method 2: Filter via Configuration Property

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> { "-y", "@playwright/mcp" },
    AllowedTools = new List<string> 
    { 
        "playwright_navigate",
        "playwright_screenshot",
        "playwright_click"
    }
};

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithMcpClientFactory(mcpFactory)
    .WithMcp(mcpConfig)  // Filter is in the configuration
    .BuildChatAgentAsync();
```

### Method 3: No Filter (All Tools)

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> { "-y", "@playwright/mcp" }
};

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithMcpClientFactory(mcpFactory)
    .WithMcp(mcpConfig)  // No filter - all tools exposed
    .BuildChatAgentAsync();
```

## Benefits

1. **Reduced Context Size**: Only relevant tools are sent to the model
2. **Lower Token Usage**: Fewer tool definitions = fewer tokens consumed
3. **Less Confusion**: Agent focuses on tools it actually needs
4. **Better Performance**: Smaller context = faster responses

## Example: Playwright MCP Server

The Playwright MCP server provides 21+ tools. If you only need basic navigation and screenshots:

```csharp
// Instead of all 21 tools, only expose 3
var allowedTools = new List<string> 
{ 
    "playwright_navigate",
    "playwright_screenshot",
    "playwright_click"
};

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, AnthropicModels.ClaudeSonnet45)
    .WithMcpClientFactory(mcpFactory)
    .WithMcp(playwrightConfig, allowedTools)
    .BuildChatAgentAsync();

// Agent now only has access to these 3 tools
await agent.SendAsync("Go to example.com and take a screenshot");
```

## Priority

If you specify `AllowedTools` in both the configuration AND the builder parameter, the builder parameter takes precedence:

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> { "-y", "@playwright/mcp" },
    AllowedTools = new List<string> { "tool1", "tool2" }  // Ignored
};

var builderFilter = new List<string> { "tool3", "tool4" };  // This is used

var agent = await new AgentBuilder()
    .WithMcp(mcpConfig, builderFilter)  // builderFilter overrides config.AllowedTools
    .BuildChatAgentAsync();
```
