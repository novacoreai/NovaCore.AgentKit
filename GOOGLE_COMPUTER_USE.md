# Google Gemini Computer Use Support

## Overview

The Gemini 2.5 Computer Use Preview model (`gemini-2.5-computer-use-preview-10-2025`) enables browser control agents that can interact with and automate tasks by "seeing" screenshots and "acting" through UI actions like mouse clicks and keyboard inputs.

## The Issue

When using the Computer Use model, Google requires that the `computer_use` tool configuration **must** be present in the API request, even when you're providing custom user-defined functions. Without this configuration, the API returns:

```
BadRequest - {
  "error": {
    "code": 400,
    "message": "This model requires the use of the Computer Use tool. See https://ai.google.dev/gemini-api/docs/computer-use#send-request for instructions on adding the tool.",
    "status": "INVALID_ARGUMENT"
  }
}
```

## The Solution

We've added Computer Use support to the Google provider. The implementation automatically includes the required `computer_use` tool configuration when enabled.

### Required API Request Format

According to Google's documentation, the request must include:

```python
tools=[
    types.Tool(
        computer_use=types.ComputerUse(
            environment=types.Environment.ENVIRONMENT_BROWSER,
            excluded_predefined_functions=["drag_and_drop"]  # optional
        )
    ),
    # Optional: Custom user-defined functions
    types.Tool(function_declarations=custom_functions)
]
```

### Our Implementation

The library now sends the correct format:

```json
{
  "tools": [
    {
      "computer_use": {
        "environment": "ENVIRONMENT_BROWSER",
        "excluded_predefined_functions": ["drag_and_drop"]
      }
    },
    {
      "functionDeclarations": [
        // Your custom tools here
      ]
    }
  ]
}
```

## Usage

### Option 1: Convenience Method (Recommended)

```csharp
var agent = await new AgentBuilder()
    .UseGoogleComputerUse(
        apiKey: "your-api-key",
        excludedFunctions: new List<string> { "drag_and_drop", "hover_at" })
    .AddTool(new ClickTool(), skipDefinition: true)  // Model is pre-trained on this
    .AddTool(new TypeTextTool(), skipDefinition: true)
    .AddTool(new ScreenshotTool())  // Custom multimodal tool
    .BuildChatAgentAsync();
```

### Option 2: Manual Configuration

```csharp
var agent = await new AgentBuilder()
    .UseGoogle(options =>
    {
        options.ApiKey = "your-api-key";
        options.Model = GoogleModels.Gemini25ComputerUsePreview;
        options.EnableComputerUse = true;
        options.ComputerUseEnvironment = ComputerUseEnvironment.Browser;
        options.ExcludedComputerUseFunctions = new List<string> 
        { 
            "drag_and_drop",
            "hover_at" 
        };
    })
    .AddTool(new ClickTool(), skipDefinition: true)
    .AddTool(new TypeTextTool(), skipDefinition: true)
    .BuildChatAgentAsync();
```

## Computer Use Configuration Options

### GoogleOptions Properties

- **`EnableComputerUse`** (bool): Enable Computer Use tool (required for computer use model)
- **`ComputerUseEnvironment`** (enum): Browser or Desktop environment
- **`ExcludedComputerUseFunctions`** (List<string>): Functions to exclude from the predefined set

### ComputerUseEnvironment Enum

```csharp
public enum ComputerUseEnvironment
{
    Browser,   // ENVIRONMENT_BROWSER - Web automation
    Desktop    // ENVIRONMENT_DESKTOP - Desktop automation
}
```

## Supported Computer Use Actions

The model can request these predefined UI actions (implement client-side execution):

| Action | Description | Arguments |
|--------|-------------|-----------|
| `open_web_browser` | Opens the web browser | None |
| `wait_5_seconds` | Pauses for 5 seconds | None |
| `go_back` | Browser back button | None |
| `go_forward` | Browser forward button | None |
| `search` | Navigate to search engine | None |
| `navigate` | Go to URL | `url`: string |
| `click_at` | Click at coordinates | `x`, `y`: int (0-999) |
| `hover_at` | Hover at coordinates | `x`, `y`: int (0-999) |
| `type_text_at` | Type text at coordinates | `x`, `y`: int, `text`: string, `press_enter`: bool, `clear_before_typing`: bool |
| `key_combination` | Press key combo | `keys`: string (e.g., "Control+C") |
| `scroll_document` | Scroll page | `direction`: "up"/"down"/"left"/"right" |
| `scroll_at` | Scroll at coordinates | `x`, `y`, `direction`, `magnitude`: int |
| `drag_and_drop` | Drag and drop | `x`, `y`, `destination_x`, `destination_y`: int |

## Using with Custom Tools

You can combine Computer Use with your own custom tools:

```csharp
var agent = await new AgentBuilder()
    .UseGoogleComputerUse(apiKey)
    .AddTool(new ClickTool(), skipDefinition: true)      // Pre-trained
    .AddTool(new TypeTextTool(), skipDefinition: true)   // Pre-trained
    .AddTool(new ScreenshotTool())                       // Custom multimodal tool
    .AddTool(new ExtractDataTool())                      // Custom function
    .BuildChatAgentAsync();
```

### Important: Use `skipDefinition: true`

For tools that the Computer Use model is pre-trained on (like `click_at`, `type_text_at`, etc.), use `skipDefinition: true` when adding them. This prevents sending duplicate tool definitions while still allowing your code to execute them.

## Coordinate System

The model outputs normalized coordinates (0-999) regardless of screen size. Your client-side code must convert these to actual pixels:

```csharp
int actualX = (int)(normalizedX / 1000.0 * screenWidth);
int actualY = (int)(normalizedY / 1000.0 * screenHeight);
```

Recommended screen size: **1440 x 900**

## Safety Considerations

The model may include a `safety_decision` in responses:

```json
{
  "function_call": {
    "name": "click_at",
    "args": {
      "x": 60,
      "y": 100,
      "safety_decision": {
        "explanation": "Clicking on a cookie consent banner",
        "decision": "require_confirmation"
      }
    }
  }
}
```

When `decision` is `require_confirmation`, you **must** prompt the user before executing the action (per Google's Terms of Service).

## Implementation Guide

### Step 1: Create Multimodal Tools

Computer Use tools **must** inherit from `MultimodalTool<TArgs>` (not `Tool<TArgs, TResult>`) and return `ToolResult` with both JSON and screenshot:

```csharp
using NovaCore.AgentKit.Core;
using System.Text.Json;

public class ClickTool : MultimodalTool<ClickArgs>
{
    private readonly IPage _page;
    private readonly GeminiCoordinateScaler _scaler;

    public override string Name => "click_at";
    public override string Description => "Clicks at a specific coordinate on the webpage. x and y are based on a 1000x1000 grid.";

    public ClickTool(IPage page)
    {
        _page = page;
        _scaler = new GeminiCoordinateScaler(page);
    }

    protected override async Task<ToolResult> ExecuteAsync(ClickArgs args, CancellationToken ct)
    {
        // 1. Scale coordinates from 1000x1000 to actual screen size
        var (actualX, actualY) = await _scaler.ScaleCoordinatesAsync(args.X, args.Y);

        // 2. Execute the action
        await _page.Mouse.ClickAsync(actualX, actualY);
        await Task.Delay(500, ct); // Wait for page to settle

        // 3. Capture new state (REQUIRED by Google)
        var screenshot = await _page.ScreenshotAsync();
        var url = _page.Url;

        // 4. Return JSON with URL + screenshot as multimodal content
        return new ToolResult
        {
            Text = JsonSerializer.Serialize(new { url }),
            AdditionalContent = new ImageMessageContent(screenshot, "image/png")
        };
    }
}

public record ClickArgs(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y
);
```

### Step 2: Coordinate Scaling Helper

```csharp
public class GeminiCoordinateScaler
{
    private readonly IPage _page;

    public GeminiCoordinateScaler(IPage page)
    {
        _page = page;
    }

    public async Task<(float X, float Y)> ScaleCoordinatesAsync(int geminiX, int geminiY)
    {
        var viewport = _page.ViewportSize ?? new PageViewportSizeResult { Width = 1440, Height = 900 };
        
        // Scale from 1000x1000 grid to actual viewport
        var actualX = (float)((geminiX / 1000.0) * viewport.Width);
        var actualY = (float)((geminiY / 1000.0) * viewport.Height);
        
        return (actualX, actualY);
    }
}
```

### Step 3: Set Up Agent

```csharp
var agent = await new AgentBuilder()
    .UseGoogleComputerUse(apiKey)
    // Add ALL Computer Use tools with skipDefinition: true
    .AddTool(new ClickTool(page), skipDefinition: true)
    .AddTool(new TypeTextTool(page), skipDefinition: true)
    .AddTool(new NavigateTool(page), skipDefinition: true)
    .AddTool(new ScrollDocumentTool(page), skipDefinition: true)
    .AddTool(new KeyPressTool(page), skipDefinition: true)
    // Optional: Add custom tools (without skipDefinition)
    .AddTool(new ExtractDataTool())
    .WithSystemPrompt("You are a browser automation assistant.")
    .BuildChatAgentAsync();
```

### Step 4: Send Task with Initial Screenshot

```csharp
// Navigate to starting page
await page.GotoAsync("https://www.google.com");

// Capture initial screenshot
var screenshot = await page.ScreenshotAsync();

// Send task with screenshot
var response = await agent.SendAsync(
    "Search for 'Playwright documentation' and click the first result",
    new List<FileAttachment> { FileAttachment.FromBytes(screenshot, "image/png") });

Console.WriteLine($"Result: {response.Text}");
```

## Critical Requirements

### ✅ DO:

1. **Always return URL in tool response JSON**
   ```csharp
   Text = JsonSerializer.Serialize(new { url = currentUrl })
   ```

2. **Always include screenshot as AdditionalContent**
   ```csharp
   AdditionalContent = new ImageMessageContent(screenshot, "image/png")
   ```

3. **Use `skipDefinition: true` for predefined Computer Use tools**
   ```csharp
   .AddTool(new ClickTool(page), skipDefinition: true)
   ```

4. **Inherit from `MultimodalTool<TArgs>`** (not `Tool<TArgs, TResult>`)
   ```csharp
   public class ClickTool : MultimodalTool<ClickArgs>
   ```

5. **Wait for page to settle after actions**
   ```csharp
   await Task.Delay(500, ct);
   ```

6. **Use `.WithMaxMultimodalMessages(1)` to limit screenshot history**
   ```csharp
   .WithMaxMultimodalMessages(1)  // Keeps only the most recent screenshot
   ```
   - This strips old images but preserves tool call/result structure
   - Prevents excessive token usage from multiple screenshots

### ❌ DON'T:

1. **Don't return screenshot as JSON string**
   ```csharp
   // ❌ WRONG
   return new ToolResult { Text = JsonSerializer.Serialize(new { url, screenshot = base64 }) };
   
   // ✅ CORRECT
   return new ToolResult 
   { 
       Text = JsonSerializer.Serialize(new { url }),
       AdditionalContent = new ImageMessageContent(screenshotBytes, "image/png")
   };
   ```

2. **Don't forget to scale coordinates**
   ```csharp
   // ❌ WRONG - using Gemini coordinates directly
   await page.Mouse.ClickAsync(args.X, args.Y);
   
   // ✅ CORRECT - scale from 1000x1000 to actual pixels
   var (actualX, actualY) = await _scaler.ScaleCoordinatesAsync(args.X, args.Y);
   await page.Mouse.ClickAsync(actualX, actualY);
   ```

3. **Don't manually manipulate conversation history**
   - The library handles history management correctly
   - Use `.WithMaxMultimodalMessages(N)` to limit screenshots instead

## Troubleshooting

### Error: "Each Function Call must be matched to a Function Response by name"

**Cause:** The tool result's CallId doesn't match any previous tool call.

**Solutions:**
1. Don't manually remove messages from conversation history
2. Ensure you're not creating tool result messages with incorrect CallIds
3. Update to the latest version of the library (fixed in recent versions)

**Debug:** The library will throw a detailed error:
```
Tool result CallId 'abc123' not found in tool call mapping.
Available CallIds: xyz789, def456
This usually means the tool result message has an incorrect ToolCallId.
```

**Note:** `.WithMaxMultimodalMessages(N)` now correctly preserves tool structure while removing old images.

### Error: "Computer Use Model requires function response to contain the URL"

**Cause:** Tool response JSON doesn't include the `url` field.

**Solution:** Always include `url` in your response:
```csharp
Text = JsonSerializer.Serialize(new { url = _page.Url })
```

### Error: "This model requires the use of the Computer Use tool"

**Cause:** Not using `.UseGoogleComputerUse()` or `EnableComputerUse = true`.

**Solution:** Use the convenience method:
```csharp
.UseGoogleComputerUse(apiKey)
```

### Browser Actions Not Working

**Cause:** Coordinates not scaled correctly or page not settled.

**Solutions:**
1. Use `GeminiCoordinateScaler` to convert 1000x1000 → actual pixels
2. Add delays after actions: `await Task.Delay(500, ct);`
3. Wait for network idle: `await page.WaitForLoadStateAsync(LoadState.NetworkIdle);`

## Example: Complete Browser Automation Agent

```csharp
// 1. Set up Playwright browser
var playwright = await Playwright.CreateAsync();
var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
var page = await browser.NewPageAsync(new() 
{ 
    ViewportSize = new() { Width = 1440, Height = 900 } 
});

// 2. Create agent with all Computer Use tools
var agent = await new AgentBuilder()
    .UseGoogleComputerUse(apiKey)
    .AddTool(new OpenBrowserTool(page), skipDefinition: true)
    .AddTool(new NavigateTool(page), skipDefinition: true)
    .AddTool(new SearchTool(page), skipDefinition: true)
    .AddTool(new ClickTool(page), skipDefinition: true)
    .AddTool(new TypeTextTool(page), skipDefinition: true)
    .AddTool(new KeyPressTool(page), skipDefinition: true)
    .AddTool(new ScrollDocumentTool(page), skipDefinition: true)
    .AddTool(new ScrollAtTool(page), skipDefinition: true)
    .AddTool(new HoverTool(page), skipDefinition: true)
    .AddTool(new GoBackTool(page), skipDefinition: true)
    .AddTool(new GoForwardTool(page), skipDefinition: true)
    .AddTool(new WaitTool(page), skipDefinition: true)
    .WithSystemPrompt("You are a browser automation assistant. Complete tasks step by step.")
    .BuildChatAgentAsync();

// 3. Navigate and send task
await page.GotoAsync("https://www.google.com");
var screenshot = await page.ScreenshotAsync();

var response = await agent.SendAsync(
    "Search for 'Playwright documentation' on Google and click the first result.",
    new List<FileAttachment> { FileAttachment.FromBytes(screenshot, "image/png") });

Console.WriteLine($"Task completed: {response.Text}");
Console.WriteLine($"Final URL: {page.Url}");

// 4. Cleanup
await agent.DisposeAsync();
await browser.CloseAsync();
```

## Implementation Changes

### Files Modified

1. **`GoogleOptions.cs`**
   - Added `EnableComputerUse` property
   - Added `ComputerUseEnvironment` enum property
   - Added `ExcludedComputerUseFunctions` list property

2. **`GoogleChatClient.cs`**
   - Updated `BuildTools()` to include `computer_use` tool configuration when enabled
   - Maintains support for custom function declarations alongside Computer Use

3. **`GoogleAgentBuilderExtensions.cs`**
   - Added `UseGoogleComputerUse()` convenience method

4. **`GoogleModels.cs`**
   - Already had `Gemini25ComputerUsePreview` constant defined

## Testing

To test Computer Use functionality:

```csharp
[Fact]
public async Task ComputerUse_SendsCorrectToolConfiguration()
{
    var agent = await new AgentBuilder()
        .UseGoogleComputerUse(apiKey, new List<string> { "drag_and_drop" })
        .AddTool(new ClickTool(), skipDefinition: true)
        .BuildChatAgentAsync();
    
    // Verify the request includes computer_use tool
    // and excluded_predefined_functions
}
```

## References

- [Google Computer Use Documentation](https://ai.google.dev/gemini-api/docs/computer-use)
- [Reference Implementation](https://github.com/google/computer-use-preview/)
- [Browserbase Demo](http://gemini.browserbase.com)
