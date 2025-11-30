using Microsoft.Playwright;

namespace NovaCore.AgentKit.Tests.Tools;

/// <summary>
/// Scales Gemini's 1000x1000 coordinate grid to actual screen dimensions
/// </summary>
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
