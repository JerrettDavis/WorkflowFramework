using Microsoft.Playwright;

namespace WorkflowFramework.Dashboard.UITests.Support;

/// <summary>
/// Manages the Playwright browser lifecycle.
/// </summary>
public sealed class PlaywrightFixture : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Browser not initialized");

    public async Task InitializeAsync()
    {
        if (_playwright is not null) return;

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1400, Height = 900 },
            IgnoreHTTPSErrors = true
        });
        return await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }
}
