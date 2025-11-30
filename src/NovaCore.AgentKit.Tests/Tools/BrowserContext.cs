using Microsoft.Playwright;

namespace NovaCore.AgentKit.Tests.Tools;

/// <summary>
/// Interface matching Browserbase session for compatibility
/// </summary>
public interface IBrowserbaseSession
{
    IPage Page { get; }
    Task<byte[]> TakeScreenshotAsync();
}

/// <summary>
/// Manages Playwright browser lifecycle for Computer Use tests
/// </summary>
public class BrowserContext : IBrowserbaseSession, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private readonly bool _headless;

    public BrowserContext(bool headless = true)
    {
        _headless = headless;
    }

    public IPage Page => _page ?? throw new InvalidOperationException("Browser not initialized. Call GetPageAsync first.");

    public async Task<IPage> GetPageAsync()
    {
        if (_page != null) return _page;

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = _headless,
            Args = _headless ? new[] { "--window-size=1440,900" } : null,
            SlowMo = _headless ? 0 : 500 // Slow down for visual debugging when headed
        });

        _page = await _browser.NewPageAsync(new()
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
        });

        return _page;
    }

    public string GetCurrentUrl()
    {
        return _page?.Url ?? "about:blank";
    }

    public async Task<byte[]> TakeScreenshotAsync()
    {
        if (_page == null) throw new InvalidOperationException("Browser not initialized");
        return await _page.ScreenshotAsync(new() { Type = ScreenshotType.Png });
    }

    public async ValueTask DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
