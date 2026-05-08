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
        ArtifactPaths.InitializeRun();
        _playwrightFixture = new PlaywrightFixture();
        await _playwrightFixture.InitializeAsync();
    }

    [BeforeScenario]
    public async Task CreatePage()
    {
        var page = await PlaywrightFixture.NewPageAsync();
        var artifacts = new ScenarioArtifacts(
            _scenarioContext.ScenarioInfo.Title,
            ArtifactPaths.GetScenarioDirectory(_scenarioContext),
            _scenarioContext.ScenarioInfo.Tags);
        if (!IsSensitiveScenario())
        {
            await page.Context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });
        }
        _scenarioContext.Set(page);
        _scenarioContext.Set(artifacts);
    }

    [AfterScenario(Order = 20000)]
    public async Task ClosePage()
    {
        if (_scenarioContext.TryGetValue<IPage>(out var page))
        {
            var context = page.Context;
            if (_scenarioContext.TryGetValue<ScenarioArtifacts>(out var artifacts))
            {
                try
                {
                    if (!IsSensitiveScenario())
                    {
                        var tracePath = ArtifactPaths.GetScenarioArtifactPath(_scenarioContext, "traces", "trace.zip");
                        await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
                        artifacts.Add("trace", tracePath);
                    }
                }
                catch (Exception ex)
                {
                    artifacts.Add("trace-error", ex.Message);
                }
                finally
                {
                    await artifacts.WriteManifestAsync(_scenarioContext.TestError is null);
                }
            }
            await page.CloseAsync();
            await context.CloseAsync();
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

    private bool IsSensitiveScenario()
        => _scenarioContext.ScenarioInfo.Tags.Contains("sensitive", StringComparer.OrdinalIgnoreCase)
           || _scenarioContext.ScenarioInfo.Tags.Contains("no-artifacts", StringComparer.OrdinalIgnoreCase);
}
