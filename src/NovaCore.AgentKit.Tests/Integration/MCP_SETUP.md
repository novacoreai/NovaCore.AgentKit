# MCP Integration Tests Setup

## Prerequisites

To run MCP integration tests, you need to install the MCP servers.

### Microsoft Playwright MCP Server

The Playwright MCP tests use the official [Microsoft Playwright MCP server](https://github.com/microsoft/playwright-mcp) - `@playwright/mcp`.

#### Installation:

```bash
# Install globally (recommended for testing)
npm install -g @playwright/mcp

# Or the tests will use npx -y to auto-install on first run
```

#### Verify Installation:

```bash
# Check if installed
npx @playwright/mcp --version

# Or test directly
npx @playwright/mcp
```

#### Available Browser Tools:

The [Microsoft Playwright MCP](https://github.com/microsoft/playwright-mcp) provides comprehensive browser automation tools:

**Page Interaction:**
- `browser_navigate` - Navigate to a URL
- `browser_snapshot` - Capture accessibility snapshot (better than screenshot for actions)
- `browser_take_screenshot` - Take screenshots
- `browser_click` - Click elements
- `browser_type` - Type text into elements
- `browser_fill_form` - Fill multiple form fields

**Navigation:**
- `browser_navigate_back` - Go back to previous page
- `browser_tabs` - List, create, close, or select tabs

**Element Interaction:**
- `browser_hover` - Hover over elements
- `browser_select_option` - Select dropdown options
- `browser_press_key` - Press keyboard keys
- `browser_drag` - Drag and drop elements

**Information:**
- `browser_console_messages` - Get console output
- `browser_network_requests` - Monitor network requests

**Utilities:**
- `browser_file_upload` - Upload files
- `browser_handle_dialog` - Handle alerts/confirms/prompts
- `browser_wait_for` - Wait for text or time
- `browser_resize` - Resize browser window
- `browser_close` - Close the browser

## Running MCP Tests

### 1. Enable the Tests

Remove the `Skip` attribute from test methods in `McpPlaywrightTests.cs`:

```csharp
// Change from:
[Fact(Skip = "Integration test - requires Playwright MCP server installed")]

// To:
[Fact]
```

### 2. Run the Tests

```bash
# Run all MCP tests
dotnet test --filter "FullyQualifiedName~McpPlaywrightTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~ChatAgent_WithPlaywrightMcp_ShouldNavigateAndSearch"

# With detailed output
dotnet test --filter "FullyQualifiedName~McpPlaywrightTests" -v detailed
```

## Test Scenarios

### 1. ChatAgent with Playwright MCP - Google Search
**Test:** `ChatAgent_WithPlaywrightMcp_ShouldNavigateAndSearch`

**What it does:**
```csharp
var agent = new AgentBuilder()
    .UseGoogle(apiKey, "gemini-2.5-flash")
    .AddTools(playwrightMcpTools)  // All browser automation tools
    .BuildChatAgent();

await agent.SendAsync(
    "Navigate to google.com, search for 'cursor', " +
    "and tell me the title of the first search result.");
```

**Expected workflow:**
1. Agent uses `browser_navigate` → google.com
2. Agent uses `browser_snapshot` → analyzes the page
3. Agent uses `browser_type` → types "cursor" in search box
4. Agent uses `browser_click` → clicks search button
5. Agent uses `browser_snapshot` → extracts results
6. Agent returns the first result title

**Assertions:**
- ✅ Multiple tool calls executed
- ✅ Response mentions navigation and search steps
- ✅ Response contains "cursor" reference

### 2. ReActAgent - Autonomous Web Extraction
**Test:** `ReActAgent_WithPlaywrightMcp_ShouldCompleteComplexTask`

**What it does:**
```csharp
var agent = new AgentBuilder()
    .UseGoogle(apiKey, "gemini-2.5-flash")
    .AddTools(playwrightMcpTools)
    .WithReActConfig(cfg => cfg.MaxTurns = 30)
    .BuildReActAgent();

var result = await agent.RunAsync(
    "Go to example.com and extract the main heading text.");
```

**Expected workflow:**
1. ReAct loop: Think → Act → Observe
2. Navigate to example.com
3. Use snapshot to analyze page structure
4. Extract h1 heading
5. Call `complete_task` with result

**Assertions:**
- ✅ Task completed successfully
- ✅ Multiple tool calls
- ✅ Result contains "Example Domain"

### 3. MCP Tool Discovery
**Test:** `McpClient_DiscoverTools_ShouldReturnPlaywrightTools`

**What it does:**
- Connects to Microsoft Playwright MCP server
- Discovers all available browser automation tools
- Lists tool names and descriptions

**Expected tools (from [Microsoft Playwright MCP](https://github.com/microsoft/playwright-mcp)):**
- `browser_navigate`
- `browser_snapshot`
- `browser_click`
- `browser_type`
- `browser_take_screenshot`
- `browser_fill_form`
- `browser_tabs`
- And 20+ more browser automation tools

## Configuration

### MCP Server Command

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string>
    {
        "-y",
        "@playwright/mcp"  // Microsoft's official Playwright MCP
    }
};
```

### Browser Configuration (Optional)

The Microsoft Playwright MCP server supports configuration:

```bash
# Use specific browser
npx @playwright/mcp --browser=chromium

# Enable capabilities
npx @playwright/mcp --caps=vision,pdf,tracing

# Headless mode
npx @playwright/mcp --headless
```

To use these in tests, modify the `Arguments` list:

```csharp
Arguments = new List<string>
{
    "-y",
    "@playwright/mcp",
    "--browser=chromium",
    "--caps=vision"
}
```

## Troubleshooting

### "Command not found" or "npx not found"
- Install Node.js: https://nodejs.org/
- Ensure `npm` and `npx` are in your PATH
- Verify: `node --version` and `npx --version`

### "Package not found: @playwright/mcp"
```bash
# Install globally
npm install -g @playwright/mcp

# Or let npx download it automatically (with -y flag)
npx -y @playwright/mcp
```

### "Browser not installed"
The Microsoft Playwright MCP will prompt to install browsers on first run:
```bash
# Install browsers manually
npx @playwright/mcp install
```

Or use the `browser_install` tool from within the agent!

### Tests timeout
- Increase timeout in test configuration
- Check internet connection (tests access google.com, example.com)
- Browser automation can be slow on first run
- Consider using `--headless` flag for faster execution

### MCP connection errors
- Check stderr output for detailed error messages
- Verify the MCP server starts correctly: `npx @playwright/mcp`
- Ensure no firewall blocking localhost communication

### Permission errors
Some operations require user permission in Playwright MCP:
- The agent will need to provide element descriptions
- Use `browser_snapshot` to get element references
- Follow the permission model for sensitive operations

## Advanced Usage

### Custom Browser Configuration

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string>
    {
        "-y",
        "@playwright/mcp",
        "--browser=firefox",      // Use Firefox
        "--headless",             // Headless mode
        "--caps=vision,pdf"       // Enable vision and PDF capabilities
    },
    Environment = new Dictionary<string, string>
    {
        { "PLAYWRIGHT_BROWSERS_PATH", "C:\\browsers" }
    }
};
```

### System Prompt for Browser Automation

```csharp
.WithSystemPrompt(@"You are a browser automation expert.

When interacting with web pages:
1. Always use browser_snapshot first to see the page structure
2. Use element 'ref' values from snapshots for accurate clicking/typing
3. Wait for page loads with browser_wait_for when needed
4. Be specific about which elements you're interacting with

Available tools: browser_navigate, browser_snapshot, browser_click, browser_type, etc.")
```

### Multi-Step Browser Task Example

```csharp
var result = await agent.RunAsync(@"
Complete this task:
1. Navigate to github.com
2. Search for 'playwright'
3. Click on the first repository result
4. Extract the repository description
5. Return the description text
");
```

## Additional MCP Servers

You can test with other MCP servers as well by changing the configuration:

### Filesystem MCP
```bash
npm install -g @modelcontextprotocol/server-filesystem
```

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> 
    { 
        "-y", 
        "@modelcontextprotocol/server-filesystem",
        "--allowed-directories", 
        "C:\\temp" 
    }
};
```

### GitHub MCP
```bash
npm install -g @modelcontextprotocol/server-github
```

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> { "-y", "@modelcontextprotocol/server-github" },
    Environment = new Dictionary<string, string>
    {
        { "GITHUB_TOKEN", "your-github-token" }
    }
};
```

## Resources

- [Microsoft Playwright MCP GitHub](https://github.com/microsoft/playwright-mcp)
- [MCP NPM Package](https://www.npmjs.com/package/@playwright/mcp)
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [Playwright Documentation](https://playwright.dev/)

## Performance Tips

1. **Use headless mode** for faster execution in CI/CD
2. **Reuse MCP client** - don't create/dispose for every request
3. **Use snapshots** instead of screenshots when possible (faster)
4. **Limit turns** in ReActAgent to prevent runaway loops
5. **Cache browser installations** in CI/CD environments
