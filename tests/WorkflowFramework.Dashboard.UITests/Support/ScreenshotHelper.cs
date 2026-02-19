using Microsoft.Playwright;

namespace WorkflowFramework.Dashboard.UITests.Support;

/// <summary>
/// Captures screenshots and saves them to the docs/images/dashboard/ directory.
/// </summary>
public static class ScreenshotHelper
{
    private static readonly string OutputDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "images", "dashboard"));

    /// <summary>
    /// Takes a full-page screenshot and saves it with the given filename.
    /// </summary>
    public static async Task CaptureFullPageAsync(IPage page, string filename)
    {
        Directory.CreateDirectory(OutputDir);
        var path = Path.Combine(OutputDir, filename);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = false // Use viewport size (1400x900)
        });
    }

    /// <summary>
    /// Takes an element screenshot and saves it with the given filename.
    /// </summary>
    public static async Task CaptureElementAsync(IPage page, string selector, string filename)
    {
        Directory.CreateDirectory(OutputDir);
        var path = Path.Combine(OutputDir, filename);
        var element = page.Locator(selector);
        await element.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await element.ScreenshotAsync(new LocatorScreenshotOptions { Path = path });
    }

    /// <summary>
    /// Takes a full-page screenshot with specific dimensions.
    /// </summary>
    public static async Task CaptureWithDimensionsAsync(IPage page, string filename, int width, int height)
    {
        Directory.CreateDirectory(OutputDir);
        var path = Path.Combine(OutputDir, filename);
        await page.SetViewportSizeAsync(width, height);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path });
        // Reset to default
        await page.SetViewportSizeAsync(1400, 900);
    }
}
