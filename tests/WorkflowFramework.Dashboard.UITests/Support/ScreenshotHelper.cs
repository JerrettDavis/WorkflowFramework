using Microsoft.Playwright;
using Reqnroll;

namespace WorkflowFramework.Dashboard.UITests.Support;

/// <summary>
/// Captures screenshots into deterministic UI-test artifact directories and optionally promotes
/// selected screenshots into a docs-review folder for later curation.
/// </summary>
public static class ScreenshotHelper
{
    public static async Task<string> CaptureFullPageAsync(IPage page, ScenarioContext scenarioContext, string filename, bool promoteForDocs = false)
    {
        var path = ArtifactPaths.GetScenarioArtifactPath(scenarioContext, "screenshots", filename);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = false // Use viewport size (1400x900)
        });

        return await PromoteIfRequested(path, filename, promoteForDocs);
    }

    public static async Task<string> CaptureElementAsync(IPage page, ScenarioContext scenarioContext, string selector, string filename, bool promoteForDocs = false)
    {
        var path = ArtifactPaths.GetScenarioArtifactPath(scenarioContext, "screenshots", filename);
        var element = page.Locator(selector);
        await element.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await element.ScreenshotAsync(new LocatorScreenshotOptions { Path = path });

        return await PromoteIfRequested(path, filename, promoteForDocs);
    }

    public static async Task<string> CaptureWithDimensionsAsync(IPage page, ScenarioContext scenarioContext, string filename, int width, int height, bool promoteForDocs = false)
    {
        var path = ArtifactPaths.GetScenarioArtifactPath(scenarioContext, "screenshots", filename);
        await page.SetViewportSizeAsync(width, height);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path });
        // Reset to default
        await page.SetViewportSizeAsync(1400, 900);

        return await PromoteIfRequested(path, filename, promoteForDocs);
    }

    private static async Task<string> PromoteIfRequested(string artifactPath, string filename, bool promoteForDocs)
    {
        if (!promoteForDocs)
            return artifactPath;

        var promotedPath = ArtifactPaths.GetDocsPromotablePath(filename);
        await using var source = File.OpenRead(artifactPath);
        await using var destination = File.Create(promotedPath);
        await source.CopyToAsync(destination);
        return promotedPath;
    }
}
