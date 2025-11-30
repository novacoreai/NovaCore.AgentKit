using System.Text.Json;
using System.Text.Json.Serialization;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Tests.Tools;

// ============================================================================
// NAVIGATION TOOLS
// ============================================================================

/// <summary>
/// Gemini Computer Use tool: open_web_browser
/// </summary>
public class OpenBrowserTool : MultimodalTool<EmptyArgs>
{
    private readonly IBrowserbaseSession _browserSession;

    public override string Name => "open_web_browser";
    public override string Description => "Opens the web browser.";

    public OpenBrowserTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
    }

    protected override async Task<ToolResult> ExecuteAsync(EmptyArgs args, CancellationToken ct)
    {
        try
        {
            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = "Browser is open" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Error: {ex.Message}" };
        }
    }
}

/// <summary>
/// Gemini Computer Use tool: navigate
/// </summary>
public class NavigateTool : MultimodalTool<NavigateArgs>
{
    private readonly IBrowserbaseSession _browserSession;

    public override string Name => "navigate";
    public override string Description => "Navigates the browser directly to the specified URL.";

    public NavigateTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
    }

    protected override async Task<ToolResult> ExecuteAsync(NavigateArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Url))
            return new ToolResult { Text = "Error: url is required" };

        try
        {
            await _browserSession.Page.GotoAsync(args.Url);
            await Task.Delay(1500, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = $"Navigated to {args.Url}" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Error navigating to {args.Url}: {ex.Message}" };
        }
    }
}

/// <summary>
/// Gemini Computer Use tool: search
/// </summary>
public class SearchTool : MultimodalTool<EmptyArgs>
{
    private readonly IBrowserbaseSession _browserSession;

    public override string Name => "search";
    public override string Description => "Navigates to the default search engine's homepage (Google). Useful for starting a new search task.";

    public SearchTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
    }

    protected override async Task<ToolResult> ExecuteAsync(EmptyArgs args, CancellationToken ct)
    {
        try
        {
            await _browserSession.Page.GotoAsync("https://www.google.com");
            await Task.Delay(1000, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = "Navigated to Google search" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Error: {ex.Message}" };
        }
    }
}

/// <summary>
/// Gemini Computer Use tool: go_back
/// </summary>
public class GoBackTool : MultimodalTool<EmptyArgs>
{
    private readonly IBrowserbaseSession _browserSession;

    public override string Name => "go_back";
    public override string Description => "Navigates to the previous page in the browser's history.";

    public GoBackTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
    }

    protected override async Task<ToolResult> ExecuteAsync(EmptyArgs args, CancellationToken ct)
    {
        try
        {
            await _browserSession.Page.GoBackAsync();
            await Task.Delay(1000, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = "Navigated back" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Error going back: {ex.Message}" };
        }
    }
}

/// <summary>
/// Gemini Computer Use tool: go_forward
/// </summary>
public class GoForwardTool : MultimodalTool<EmptyArgs>
{
    private readonly IBrowserbaseSession _browserSession;

    public override string Name => "go_forward";
    public override string Description => "Navigates to the next page in the browser's history.";

    public GoForwardTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
    }

    protected override async Task<ToolResult> ExecuteAsync(EmptyArgs args, CancellationToken ct)
    {
        try
        {
            await _browserSession.Page.GoForwardAsync();
            await Task.Delay(1000, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = "Navigated forward" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Error going forward: {ex.Message}" };
        }
    }
}

/// <summary>
/// Gemini Computer Use tool: wait_5_seconds
/// </summary>
public class WaitTool : MultimodalTool<EmptyArgs>
{
    private readonly IBrowserbaseSession _browserSession;

    public override string Name => "wait_5_seconds";
    public override string Description => "Pauses execution for 5 seconds to allow dynamic content to load or animations to complete.";

    public WaitTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
    }

    protected override async Task<ToolResult> ExecuteAsync(EmptyArgs args, CancellationToken ct)
    {
        try
        {
            await Task.Delay(5000, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = "Waited 5 seconds" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Error waiting: {ex.Message}" };
        }
    }
}

// ============================================================================
// ARGUMENT TYPES
// ============================================================================

public record EmptyArgs();

public record NavigateArgs(
    [property: JsonPropertyName("url")] string Url
);
