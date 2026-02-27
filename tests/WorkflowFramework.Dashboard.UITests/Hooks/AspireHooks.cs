using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Support;

namespace WorkflowFramework.Dashboard.UITests.Hooks;

[Binding]
public sealed class AspireHooks
{
    private static DashboardFixture? _fixture;

    internal static DashboardFixture Fixture =>
        _fixture ?? throw new InvalidOperationException("Dashboard fixture not initialized");

    [BeforeTestRun]
    public static async Task StartAspire()
    {
        // Delete stale SQLite DB to prevent test pollution from previous runs
        var dbPath = Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
            "src", "WorkflowFramework.Dashboard.Api", "dashboard.db");
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(dbPath)!, "dashboard.db*"))
        {
            try { File.Delete(file); } catch { /* may be locked */ }
        }

        _fixture = new DashboardFixture();
        await _fixture.StartAsync();
    }

    [AfterTestRun]
    public static async Task StopAspire()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }
}
