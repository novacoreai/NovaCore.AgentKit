using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Tests.Tools;

// ============================================================================
// MOUSE INTERACTION TOOLS
// ============================================================================

/// <summary>
/// Gemini Computer Use tool: click_at
/// </summary>
public class MouseClickTool : MultimodalTool<ClickAtArgs>
{
    private readonly IBrowserbaseSession _browserSession;
    private readonly GeminiCoordinateScaler _scaler;

    public override string Name => "click_at";
    public override string Description => "Clicks at a specific coordinate on the webpage. The x and y values are based on a 1000x1000 grid and are scaled to the screen dimensions.";

    public MouseClickTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
        _scaler = new GeminiCoordinateScaler(browserSession.Page);
    }

    protected override async Task<ToolResult> ExecuteAsync(ClickAtArgs args, CancellationToken ct)
    {
        try
        {
            var (actualX, actualY) = await _scaler.ScaleCoordinatesAsync(args.X, args.Y);

            await _browserSession.Page.Mouse.ClickAsync(actualX, actualY, new MouseClickOptions
            {
                Button = MouseButton.Left
            });

            await Task.Delay(500, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = $"Clicked at ({args.X}, {args.Y})" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Failed to click at ({args.X}, {args.Y}): {ex.Message}" };
        }
    }
}

/// <summary>
/// Gemini Computer Use tool: hover_at
/// </summary>
public class MouseMoveTool : MultimodalTool<HoverAtArgs>
{
    private readonly IBrowserbaseSession _browserSession;
    private readonly GeminiCoordinateScaler _scaler;

    public override string Name => "hover_at";
    public override string Description => "Hovers the mouse at a specific coordinate on the webpage. Useful for revealing sub-menus. x and y are based on a 1000x1000 grid.";

    public MouseMoveTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
        _scaler = new GeminiCoordinateScaler(browserSession.Page);
    }

    protected override async Task<ToolResult> ExecuteAsync(HoverAtArgs args, CancellationToken ct)
    {
        try
        {
            var (actualX, actualY) = await _scaler.ScaleCoordinatesAsync(args.X, args.Y);

            await _browserSession.Page.Mouse.MoveAsync(actualX, actualY);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = $"Hovered at ({args.X}, {args.Y})" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Failed to hover at ({args.X}, {args.Y}): {ex.Message}" };
        }
    }
}

/// <summary>
/// Gemini Computer Use tool: drag_and_drop
/// </summary>
public class DragAndDropTool : MultimodalTool<DragDropArgs>
{
    private readonly IBrowserbaseSession _browserSession;
    private readonly GeminiCoordinateScaler _scaler;

    public override string Name => "drag_and_drop";
    public override string Description => "Drags an element from a starting coordinate (x, y) and drops it at a destination coordinate (destination_x, destination_y). All coordinates are based on a 1000x1000 grid.";

    public DragAndDropTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
        _scaler = new GeminiCoordinateScaler(browserSession.Page);
    }

    protected override async Task<ToolResult> ExecuteAsync(DragDropArgs args, CancellationToken ct)
    {
        try
        {
            var (startX, startY) = await _scaler.ScaleCoordinatesAsync(args.X, args.Y);
            var (endX, endY) = await _scaler.ScaleCoordinatesAsync(args.DestinationX, args.DestinationY);

            await _browserSession.Page.Mouse.MoveAsync(startX, startY);
            await _browserSession.Page.Mouse.DownAsync();
            await Task.Delay(100, ct);
            await _browserSession.Page.Mouse.MoveAsync(endX, endY, new() { Steps = 10 });
            await Task.Delay(100, ct);
            await _browserSession.Page.Mouse.UpAsync();

            await Task.Delay(500, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = $"Dragged from ({args.X}, {args.Y}) to ({args.DestinationX}, {args.DestinationY})" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Error in drag and drop: {ex.Message}" };
        }
    }
}

// ============================================================================
// ARGUMENT TYPES
// ============================================================================

public record ClickAtArgs(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y
);

public record HoverAtArgs(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y
);

public record DragDropArgs(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("destination_x")] int DestinationX,
    [property: JsonPropertyName("destination_y")] int DestinationY
);
