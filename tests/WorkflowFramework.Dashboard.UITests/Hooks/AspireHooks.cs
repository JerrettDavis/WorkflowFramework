using System.Diagnostics;
using Reqnroll;
using WorkflowFramework.Dashboard.UITests.Support;

namespace WorkflowFramework.Dashboard.UITests.Hooks;

[Binding]
public sealed class AspireHooks
{
    private static DashboardFixture? _fixture;
    private static int[]? _preExistingDotnetPids;

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

        // Kill any orphaned dcp processes from previous test runs
        foreach (var proc in Process.GetProcessesByName("dcp"))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
        }

        // Snapshot existing dotnet processes so we don't kill them in cleanup
        _preExistingDotnetPids = Process.GetProcessesByName("dotnet")
            .Select(p => p.Id).ToArray();

        _fixture = new DashboardFixture();
        await _fixture.StartAsync();
    }

    [AfterTestRun]
    public static async Task StopAspire()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();

        // Kill any orphaned dotnet processes spawned by Aspire DCP
        // that weren't cleaned up by DisposeAsync
        if (_preExistingDotnetPids is not null)
        {
            foreach (var proc in Process.GetProcessesByName("dotnet"))
            {
                if (_preExistingDotnetPids.Contains(proc.Id)) continue;
                try { proc.Kill(entireProcessTree: true); } catch { }
            }
        }

        // Also kill any lingering dcp processes
        foreach (var proc in Process.GetProcessesByName("dcp"))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
        }
    }
}
