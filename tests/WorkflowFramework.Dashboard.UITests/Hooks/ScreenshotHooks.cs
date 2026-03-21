using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Support;

namespace WorkflowFramework.Dashboard.UITests.Hooks;

[Binding]
public sealed class ScreenshotHooks
{
    private readonly ScenarioContext _scenarioContext;

    public ScreenshotHooks(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    /// <summary>
    /// On failure, capture a screenshot and page HTML for diagnostics.
    /// </summary>
    [AfterScenario(Order = 5000)]
    public async Task CaptureFailureDiagnostics()
    {
        if (_scenarioContext.TestError is null) return;
        if (!_scenarioContext.TryGetValue<IPage>(out var page)) return;
        if (IsSensitiveScenario()) return;

        var safeName = _scenarioContext.ScenarioInfo.Title
            .Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
        var screenshotPath = ArtifactPaths.GetScenarioArtifactPath(_scenarioContext, "failure", $"{safeName}.png");
        var htmlPath = ArtifactPaths.GetScenarioArtifactPath(_scenarioContext, "failure", $"{safeName}.html");

        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });
            var html = await page.ContentAsync();
            await File.WriteAllTextAsync(htmlPath, html);
            if (_scenarioContext.TryGetValue<ScenarioArtifacts>(out var artifacts))
            {
                artifacts.Add("failure-screenshot", screenshotPath);
                artifacts.Add("failure-html", htmlPath);
            }
            Console.WriteLine($"[DIAG] Failure artifacts saved to {Path.GetDirectoryName(screenshotPath)}");
            Console.WriteLine($"[DIAG] Page URL: {page.Url}");
            Console.WriteLine($"[DIAG] Page title: {await page.TitleAsync()}");

            // Query server-side circuit stats
            try
            {
                using var client = AspireHooks.Fixture.CreateApiClient();
                // Point at the web server's diagnostics endpoint
                using var webClient = new HttpClient { BaseAddress = new Uri(AspireHooks.Fixture.WebBaseUrl) };
                var stats = await webClient.GetStringAsync("/diagnostics/circuits");
                Console.WriteLine($"[DIAG] Circuit stats: {stats}");
            }
            catch (Exception statsEx)
            {
                Console.WriteLine($"[DIAG] Circuit stats unavailable: {statsEx.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DIAG] Failed to capture diagnostics: {ex.Message}");
        }
    }

    /// <summary>
    /// After each scenario, capture screenshots based on @screenshot:xxx tags.
    /// </summary>
    [AfterScenario(Order = 10000)]
    public async Task CaptureTaggedScreenshots()
    {
        if (!_scenarioContext.TryGetValue<IPage>(out var page)) return;
        if (_scenarioContext.TestError is not null) return; // Skip on failure

        var tags = _scenarioContext.ScenarioInfo.Tags;
        foreach (var tag in tags)
        {
            if (!tag.StartsWith("screenshot:", StringComparison.OrdinalIgnoreCase)) continue;

            var name = tag["screenshot:".Length..];
            var filename = $"{name}.png";

            // Element-specific screenshots for known panels
            switch (name)
            {
                case "step-palette":
                    await CaptureElementOrFallback(page, "[data-testid='step-palette']", filename);
                    break;
                case "properties-panel":
                    await CaptureElementOrFallback(page, "[data-testid='properties-panel']", filename);
                    break;
                case "toolbar":
                    await CaptureElementOrFallback(page, "[data-testid='toolbar']", filename);
                    break;
                case "validation-badge":
                    await CaptureElementOrFallback(page, "[data-testid='validation-badge']", filename);
                    break;
                case "validation-panel":
                    await CaptureElementOrFallback(page, "[data-testid='validation-panel']", filename);
                    break;
                case "execution-panel":
                    await CaptureElementOrFallback(page, "[data-testid='execution-panel']", filename);
                    break;
                case "template-browser":
                    await CaptureElementOrFallback(page, "[data-testid='template-browser']", filename);
                    break;
                case "trigger-panel":
                case "trigger-add-form":
                case "trigger-configured":
                    await CaptureElementOrFallback(page, "[data-testid='trigger-panel']", filename);
                    break;
                case "login-page":
                case "login-form":
                    await CaptureElementOrFallback(page, "[data-testid='login-username']", filename);
                    break;
                case "register-page":
                case "register-form":
                    await CaptureElementOrFallback(page, "[data-testid='register-username']", filename);
                    break;
                case "toolbar-import-export":
                    await CaptureElementOrFallback(page, "[data-testid='toolbar']", filename);
                    break;
                case "shortcuts-modal":
                case "help-modal":
                    await CaptureElementOrFallback(page, "[data-testid='shortcuts-modal']", filename);
                    break;
                case "workflow-list":
                case "open-workflow-dialog":
                    await CaptureElementOrFallback(page, "[data-testid='workflow-list']", filename);
                    break;
                default:
                    await CaptureAndTrackFullPage(page, filename);
                    break;
            }
        }
    }

    private async Task CaptureElementOrFallback(IPage page, string selector, string filename)
    {
        try
        {
            var loc = page.Locator(selector).First;
            if (await loc.IsVisibleAsync())
            {
                var promotedPath = await ScreenshotHelper.CaptureElementAsync(page, _scenarioContext, selector, filename, promoteForDocs: true);
                TrackScreenshot(filename, promotedPath);
                return;
            }
        }
        catch
        {
            // Fallback to full page
        }
        await CaptureAndTrackFullPage(page, filename);
    }

    private async Task CaptureAndTrackFullPage(IPage page, string filename)
    {
        var promotedPath = await ScreenshotHelper.CaptureFullPageAsync(page, _scenarioContext, filename, promoteForDocs: true);
        TrackScreenshot(filename, promotedPath);
    }

    private void TrackScreenshot(string filename, string promotedPath)
    {
        if (!_scenarioContext.TryGetValue<ScenarioArtifacts>(out var artifacts))
            return;

        var artifactPath = ArtifactPaths.GetScenarioArtifactPath(_scenarioContext, "screenshots", filename);
        artifacts.Add("screenshot", artifactPath, promotedPath);
    }

    private bool IsSensitiveScenario()
        => _scenarioContext.ScenarioInfo.Tags.Contains("sensitive", StringComparer.OrdinalIgnoreCase)
           || _scenarioContext.ScenarioInfo.Tags.Contains("no-artifacts", StringComparer.OrdinalIgnoreCase);
}
