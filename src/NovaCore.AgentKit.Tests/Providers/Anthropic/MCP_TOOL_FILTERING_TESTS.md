# MCP Tool Filtering Tests

## Overview

Tests for the MCP tool filtering functionality that allows selective exposure of tools from MCP servers to agents.

## Test File

`McpToolFilteringTests.cs`

## Test Cases

### 1. `McpToolFiltering_WithAllowedTools_ShouldOnlyExposeFilteredTools`

**Purpose**: Verify that when a tool filter is applied via the builder parameter, only the specified tools are available to the agent.

**Setup**:
- MCP Server: Playwright (`@playwright/mcp`)
- Allowed Tools: `playwright_navigate`, `playwright_screenshot`
- Task: "Navigate to example.com and take a screenshot"

**Expected Behavior**:
- Agent successfully completes the task using only the 2 filtered tools
- Other Playwright tools (20+) are not available

**Assertions**:
- Task completes successfully
- Agent uses only the allowed tools

---

### 2. `McpToolFiltering_ViaConfiguration_ShouldFilterTools`

**Purpose**: Verify that tool filtering works when specified in the `McpConfiguration` object.

**Setup**:
- MCP Server: Playwright (`@playwright/mcp`)
- Allowed Tools (via config): `playwright_navigate`, `playwright_screenshot`, `playwright_click`
- Task: "Go to example.com, click any link, and take a screenshot"

**Expected Behavior**:
- Agent has access to only the 3 tools specified in configuration
- Task completes successfully using the filtered tools

**Assertions**:
- Task completes successfully
- Configuration-based filtering works correctly

---

### 3. `McpToolFiltering_BuilderParameterOverridesConfiguration`

**Purpose**: Verify that the builder parameter takes precedence over configuration when both specify allowed tools.

**Setup**:
- MCP Server: Playwright (`@playwright/mcp`)
- Config Allowed Tools: `playwright_navigate`, `playwright_click`
- Builder Allowed Tools: `playwright_navigate`, `playwright_screenshot`
- Task: "Navigate to example.com and take a screenshot"

**Expected Behavior**:
- Builder parameter overrides configuration
- Agent has access to `navigate` and `screenshot` (not `click`)
- Task succeeds because screenshot is available (from builder, not config)

**Assertions**:
- Task completes successfully
- Builder parameter takes precedence

---

### 4. `McpToolDiscovery_WithFilter_ShouldReturnOnlyAllowedTools`

**Purpose**: Verify that tool discovery returns only the filtered tools when a filter is applied.

**Setup**:
- MCP Server: Playwright (`@playwright/mcp`)
- Allowed Tools: `playwright_navigate`, `playwright_screenshot`, `playwright_click`

**Expected Behavior**:
- Tool discovery returns exactly 3 tools
- Only the specified tools are present
- Other Playwright tools are filtered out

**Assertions**:
- Exactly 3 tools discovered
- Contains: `playwright_navigate`, `playwright_screenshot`, `playwright_click`
- Does NOT contain: `playwright_fill`, `playwright_evaluate`, etc.

---

### 5. `McpToolDiscovery_WithoutFilter_ShouldReturnAllTools`

**Purpose**: Verify that without a filter, all tools from the MCP server are discovered.

**Setup**:
- MCP Server: Playwright (`@playwright/mcp`)
- No filter applied

**Expected Behavior**:
- Tool discovery returns all available tools (20+)
- No filtering occurs

**Assertions**:
- More than 10 tools discovered
- All Playwright MCP tools are available

---

## Running the Tests

### Prerequisites

1. Node.js and npm installed
2. Playwright MCP server available:
   ```bash
   npm install -g @playwright/mcp
   ```

### Run All Filtering Tests

```bash
dotnet test --filter "FullyQualifiedName~McpToolFilteringTests"
```

### Run Specific Test

```bash
dotnet test --filter "FullyQualifiedName~McpToolFiltering_WithAllowedTools_ShouldOnlyExposeFilteredTools"
```

### Run with Detailed Output

```bash
dotnet test --filter "FullyQualifiedName~McpToolFilteringTests" -v detailed
```

## Configuration Required

Tests require `testconfig.json` with Anthropic API key:

```json
{
  "Providers": {
    "Anthropic": {
      "ApiKey": "your-api-key",
      "Model": "claude-sonnet-4-20250514"
    }
  }
}
```

## Test Pattern

All tests follow the standard pattern:

1. **Arrange**: Configure MCP with tool filter
2. **Act**: Build agent or discover tools
3. **Assert**: Verify filtering behavior
4. **Cleanup**: Dispose resources

## Key Concepts Tested

### Tool Filtering Methods

**Method 1: Builder Parameter**
```csharp
var allowedTools = new List<string> { "tool1", "tool2" };
.WithMcp(mcpConfig, allowedTools)
```

**Method 2: Configuration Property**
```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> { "-y", "@playwright/mcp" },
    AllowedTools = new List<string> { "tool1", "tool2" }
};
.WithMcp(mcpConfig)
```

### Precedence Rules

When both configuration and builder parameter specify `AllowedTools`:
- **Builder parameter takes precedence**
- Configuration's `AllowedTools` is ignored
- This allows runtime override of configuration

### No Filter Behavior

When `AllowedTools` is:
- `null` → All tools exposed
- Empty list → All tools exposed
- Non-empty list → Only specified tools exposed

## Benefits of Tool Filtering

1. **Reduced Context Size**: Fewer tool definitions sent to LLM
2. **Lower Token Usage**: Smaller prompts = lower costs
3. **Less Confusion**: Agent focuses on relevant tools only
4. **Better Performance**: Faster responses with smaller context

## Example Output

```
🔵 Turn 1 | Navigate to example.com and take a screenshot
  → LLM | 2 msgs, 2 tools
  ← LLM | 1234tok, 1500ms, 2 tool calls
    🔧 playwright_navigate
    ✓ playwright_navigate | 800ms | Navigated to https://example.com
    🔧 playwright_screenshot
    ✓ playwright_screenshot | 300ms | Screenshot captured
✓ Turn 1 | 2.60s | 1 LLM calls
Success: True
Answer: I navigated to example.com and captured a screenshot...
Turns: 1
Duration: 2.6s
```

## Troubleshooting

### Test Fails: "Command not found: npx"
- Install Node.js: https://nodejs.org/
- Verify: `node --version` and `npx --version`

### Test Fails: "Package not found: @playwright/mcp"
```bash
npm install -g @playwright/mcp
```

### Test Timeout
- Increase timeout in test configuration
- First run may be slow (downloads browsers)
- Check internet connection

### Wrong Tools Exposed
- Verify `AllowedTools` list matches tool names exactly
- Check that builder parameter is being used if intended
- Review test output for discovered tools

## Related Documentation

- [MCP_TOOL_FILTERING_EXAMPLE.md](../../../../../MCP_TOOL_FILTERING_EXAMPLE.md) - Usage examples
- [MCP_SETUP.md](../../Integration/MCP_SETUP.md) - MCP integration setup
- [Playwright MCP GitHub](https://github.com/microsoft/playwright-mcp) - Official server
