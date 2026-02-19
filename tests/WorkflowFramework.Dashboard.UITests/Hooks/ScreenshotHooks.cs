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
