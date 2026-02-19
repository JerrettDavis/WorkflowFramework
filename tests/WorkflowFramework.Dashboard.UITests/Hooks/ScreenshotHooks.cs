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
    [AfterScenario]
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
                    await CaptureElementOrFallback(page, "[data-testid='step-palette'], .step-palette, .sidebar-left", filename);
                    break;
                case "properties-panel":
                    await CaptureElementOrFallback(page, "[data-testid='properties-panel'], .properties-panel, .sidebar-right", filename);
                    break;
                case "toolbar":
                    await CaptureElementOrFallback(page, "[data-testid='toolbar'], .toolbar, header", filename);
                    break;
                case "validation-badge":
                    await CaptureElementOrFallback(page, "[data-testid='validation-badge'], .validation-badge, .error-badge", filename);
                    break;
                case "execution-panel":
                    await CaptureElementOrFallback(page, "[data-testid='execution-panel'], .execution-panel, .bottom-panel", filename);
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
            // Try each selector separated by comma
            var selectors = selector.Split(',', StringSplitOptions.TrimEntries);
            foreach (var s in selectors)
            {
                var loc = page.Locator(s).First;
                if (await loc.IsVisibleAsync())
                {
                    await ScreenshotHelper.CaptureElementAsync(page, s, filename);
                    return;
                }
            }
        }
        catch
        {
            // Fallback to full page
        }
        await ScreenshotHelper.CaptureFullPageAsync(page, filename);
    }
}
