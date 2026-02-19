using Microsoft.Playwright;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Support;

namespace WorkflowFramework.Dashboard.UITests.Hooks;

[Binding]
public sealed class BrowserHooks
{
    private static PlaywrightFixture? _playwrightFixture;

    internal static PlaywrightFixture PlaywrightFixture =>
        _playwrightFixture ?? throw new InvalidOperationException("Playwright not initialized");

    private readonly ScenarioContext _scenarioContext;

    public BrowserHooks(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    [BeforeTestRun]
    public static async Task InitPlaywright()
    {
        _playwrightFixture = new PlaywrightFixture();
        await _playwrightFixture.InitializeAsync();
    }

    [BeforeScenario]
    public async Task CreatePage()
    {
        var page = await PlaywrightFixture.NewPageAsync();
        _scenarioContext.Set(page);
    }

    [AfterScenario]
    public async Task ClosePage()
    {
        if (_scenarioContext.TryGetValue<IPage>(out var page))
        {
            var context = page.Context;
            await page.CloseAsync();
            await context.CloseAsync();
        }
    }

    [AfterTestRun]
    public static async Task DisposePlaywright()
    {
        if (_playwrightFixture is not null)
            await _playwrightFixture.DisposeAsync();
    }
}
