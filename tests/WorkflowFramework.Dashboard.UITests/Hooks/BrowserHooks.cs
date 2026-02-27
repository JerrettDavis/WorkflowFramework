using System.Net.Http.Json;
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

    [AfterScenario(Order = 20000)]
    public async Task ClosePage()
    {
        if (_scenarioContext.TryGetValue<IPage>(out var page))
        {
            // Navigate away to force the Blazor SignalR connection to close cleanly
            try { await page.GotoAsync("about:blank", new() { Timeout = 2_000 }); }
            catch { /* best effort */ }

            var context = page.Context;
            await page.CloseAsync();
            await context.CloseAsync();
            // Allow server to process circuit disconnect
            await Task.Delay(500);
        }
    }

    /// <summary>
    /// Clean up test-created workflows to prevent test pollution.
    /// </summary>
    [AfterScenario(Order = 19000)]
    public async Task CleanupTestWorkflows()
    {
        if (_scenarioContext.TryGetValue<string>("WorkflowId", out var id))
        {
            try
            {
                using var client = AspireHooks.Fixture.CreateApiClient();
                await client.DeleteAsync($"/api/workflows/{id}");
            }
            catch { /* best effort cleanup */ }
        }
    }

    [AfterTestRun]
    public static async Task DisposePlaywright()
    {
        if (_playwrightFixture is not null)
            await _playwrightFixture.DisposeAsync();
    }
}
