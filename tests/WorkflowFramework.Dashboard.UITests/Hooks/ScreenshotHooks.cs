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

        var safeName = _scenarioContext.ScenarioInfo.Title
            .Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "test-failures");
        Directory.CreateDirectory(dir);

        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(dir, $"{safeName}.png"),
                FullPage = true
            });
            var html = await page.ContentAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, $"{safeName}.html"), html);
            Console.WriteLine($"[DIAG] Failure artifacts saved to {dir}/{safeName}.*");
            Console.WriteLine($"[DIAG] Page URL: {page.Url}");
            Console.WriteLine($"[DIAG] Page title: {await page.TitleAsync()}");
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
                    await ScreenshotHelper.CaptureFullPageAsync(page, filename);
                    break;
            }
        }
    }

    private static async Task CaptureElementOrFallback(IPage page, string selector, string filename)
    {
        try
        {
            var loc = page.Locator(selector).First;
            if (await loc.IsVisibleAsync())
            {
                await ScreenshotHelper.CaptureElementAsync(page, selector, filename);
                return;
            }
        }
        catch
        {
            // Fallback to full page
        }
        await ScreenshotHelper.CaptureFullPageAsync(page, filename);
    }
}
