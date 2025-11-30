using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Tests.Tools;

// ============================================================================
// KEYBOARD TOOLS
// ============================================================================

/// <summary>
/// Gemini Computer Use tool: type_text_at
/// </summary>
public class TypeTextTool : MultimodalTool<TypeTextAtArgs>
{
    private readonly IBrowserbaseSession _browserSession;
    private readonly GeminiCoordinateScaler _scaler;

    public override string Name => "type_text_at";
    public override string Description => "Types text at a specific coordinate, defaults to clearing the field first and pressing ENTER after typing, but these can be disabled. x and y are based on a 1000x1000 grid.";

    public TypeTextTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
        _scaler = new GeminiCoordinateScaler(browserSession.Page);
    }

    protected override async Task<ToolResult> ExecuteAsync(TypeTextAtArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Text))
            return new ToolResult { Text = "Error: text is required" };

        try
        {
            var (actualX, actualY) = await _scaler.ScaleCoordinatesAsync(args.X, args.Y);

            await _browserSession.Page.Mouse.ClickAsync(actualX, actualY, new MouseClickOptions
            {
                Button = MouseButton.Left
            });

            if (args.ClearBeforeTyping ?? true)
            {
                await _browserSession.Page.Keyboard.PressAsync("Control+A");
            }

            await _browserSession.Page.Keyboard.TypeAsync(args.Text);

            if (args.PressEnter ?? true)
            {
                await _browserSession.Page.Keyboard.PressAsync("Enter");
            }

            await Task.Delay(500, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = $"Typed '{args.Text}' at ({args.X}, {args.Y})" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Failed to type at ({args.X}, {args.Y}): {ex.Message}" };
        }
    }
}

/// <summary>
/// Gemini Computer Use tool: key_combination
/// </summary>
public class KeyPressTool : MultimodalTool<KeyCombinationArgs>
{
    private readonly IBrowserbaseSession _browserSession;

    public override string Name => "key_combination";
    public override string Description => "Press keyboard keys or combinations, such as 'Control+C' or 'Enter'. Useful for triggering actions (like submitting a form with 'Enter') or clipboard operations.";

    public KeyPressTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
    }

    protected override async Task<ToolResult> ExecuteAsync(KeyCombinationArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Keys))
            return new ToolResult { Text = "Error: keys is required" };

        try
        {
            await _browserSession.Page.Keyboard.PressAsync(args.Keys);
            await Task.Delay(500, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = $"Pressed keys: {args.Keys}" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Failed to press keys '{args.Keys}': {ex.Message}" };
        }
    }
}

// ============================================================================
// SCROLL TOOLS
// ============================================================================

/// <summary>
/// Gemini Computer Use tool: scroll_document
/// </summary>
public class MouseScrollTool : MultimodalTool<ScrollDocumentArgs>
{
    private readonly IBrowserbaseSession _browserSession;

    public override string Name => "scroll_document";
    public override string Description => "Scrolls the entire webpage 'up', 'down', 'left', or 'right'.";

    public MouseScrollTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
    }

    protected override async Task<ToolResult> ExecuteAsync(ScrollDocumentArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Direction))
            return new ToolResult { Text = "Error: direction is required" };

        try
        {
            var (deltaX, deltaY) = args.Direction.ToLower() switch
            {
                "up" => (0, -500),
                "down" => (0, 500),
                "left" => (-500, 0),
                "right" => (500, 0),
                _ => (0, 500)
            };

            await _browserSession.Page.Mouse.WheelAsync(deltaX, deltaY);
            await Task.Delay(500, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = $"Scrolled {args.Direction}" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Failed to scroll {args.Direction}: {ex.Message}" };
        }
    }
}

/// <summary>
/// Gemini Computer Use tool: scroll_at
/// </summary>
public class ScrollAtTool : MultimodalTool<ScrollAtArgs>
{
    private readonly IBrowserbaseSession _browserSession;
    private readonly GeminiCoordinateScaler _scaler;

    public override string Name => "scroll_at";
    public override string Description => "Scrolls a specific element or area at coordinate (x, y) in the specified direction by a certain magnitude. Coordinates and magnitude (default 800) are based on a 1000x1000 grid.";

    public ScrollAtTool(IBrowserbaseSession browserSession)
    {
        _browserSession = browserSession;
        _scaler = new GeminiCoordinateScaler(browserSession.Page);
    }

    protected override async Task<ToolResult> ExecuteAsync(ScrollAtArgs args, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(args.Direction))
            return new ToolResult { Text = "Error: direction is required" };

        try
        {
            var (actualX, actualY) = await _scaler.ScaleCoordinatesAsync(args.X, args.Y);
            var magnitude = args.Magnitude ?? 800;

            var viewportSize = _browserSession.Page.ViewportSize;
            var scaledMagnitude = (int)(magnitude / 1000.0 * (viewportSize?.Height ?? 900));

            await _browserSession.Page.Mouse.MoveAsync(actualX, actualY);

            var (deltaX, deltaY) = args.Direction.ToLower() switch
            {
                "up" => (0, -scaledMagnitude),
                "down" => (0, scaledMagnitude),
                "left" => (-scaledMagnitude, 0),
                "right" => (scaledMagnitude, 0),
                _ => (0, scaledMagnitude)
            };

            await _browserSession.Page.Mouse.WheelAsync(deltaX, deltaY);
            await Task.Delay(500, ct);

            var screenshotBytes = await _browserSession.TakeScreenshotAsync();
            var optimizedBytes = await ImageOptimizer.OptimizeScreenshotAsync(
                screenshotBytes, sizeReductionPercent: 80, jpegQuality: 80);

            var url = _browserSession.Page.Url;

            return new ToolResult
            {
                Text = JsonSerializer.Serialize(new { url, action = $"Scrolled {args.Direction} at ({args.X}, {args.Y})" }),
                AdditionalContent = new ImageMessageContent(
                    optimizedBytes, ImageOptimizer.GetOptimizedMimeType())
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Text = $"Error scrolling: {ex.Message}" };
        }
    }
}

// ============================================================================
// ARGUMENT TYPES
// ============================================================================

public record TypeTextAtArgs(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("press_enter")] bool? PressEnter = true,
    [property: JsonPropertyName("clear_before_typing")] bool? ClearBeforeTyping = true
);

public record KeyCombinationArgs(
    [property: JsonPropertyName("keys")] string Keys
);

public record ScrollDocumentArgs(
    [property: JsonPropertyName("direction")] string Direction
);

public record ScrollAtArgs(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("magnitude")] int? Magnitude = 800
);
