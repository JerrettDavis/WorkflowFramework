using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Diagnostics;

public class MetricsMiddlewareTests
{
    private static IWorkflowContext Ctx() => new C();
    private static IStep Step(string n = "S") => new St(n);

    [Fact]
    public async Task InvokeAsync_IncrementsTotal()
    {
        var mw = new MetricsMiddleware();
        await mw.InvokeAsync(Ctx(), Step(), _ => Task.CompletedTask);
        mw.TotalSteps.Should().Be(1);
        mw.FailedSteps.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_OnError_IncrementsFailedAndTotal()
    {
        var mw = new MetricsMiddleware();
        await Assert.ThrowsAsync<Exception>(() =>
            mw.InvokeAsync(Ctx(), Step(), _ => throw new Exception("x")));
        mw.TotalSteps.Should().Be(1);
        mw.FailedSteps.Should().Be(1);
    }

    [Fact]
    public async Task AverageDuration_IsCalculated()
    {
        var mw = new MetricsMiddleware();
        await mw.InvokeAsync(Ctx(), Step(), async _ => await Task.Delay(10));
        mw.AverageDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void AverageDuration_NoSteps_IsZero()
    {
        new MetricsMiddleware().AverageDuration.Should().Be(TimeSpan.Zero);
    }

    private class St : IStep { public St(string n) { Name = n; } public string Name { get; } public Task ExecuteAsync(IWorkflowContext c) => Task.CompletedTask; }
    private class C : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}

public class MetricsDashboardDataProviderTests
{
    [Fact]
    public void Constructor_NullMetrics_Throws()
    {
        FluentActions.Invoking(() => new MetricsDashboardDataProvider(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetSummaryAsync_ReflectsMetrics()
    {
        var metrics = new MetricsMiddleware();
        var ctx = new C();
        var step = new St("A");
        await metrics.InvokeAsync(ctx, step, _ => Task.CompletedTask);
        var provider = new MetricsDashboardDataProvider(metrics);
        var summary = await provider.GetSummaryAsync();
        summary.TotalSteps.Should().Be(1);
        summary.FailedSteps.Should().Be(0);
    }

    private class St : IStep { public St(string n) { Name = n; } public string Name { get; } public Task ExecuteAsync(IWorkflowContext c) => Task.CompletedTask; }
    private class C : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}

public class DashboardSummaryTests
{
    [Fact]
    public void Defaults()
    {
        var s = new DashboardSummary();
        s.TotalWorkflows.Should().Be(0);
        s.TotalSteps.Should().Be(0);
        s.FailedSteps.Should().Be(0);
        s.AverageStepDuration.Should().Be(TimeSpan.Zero);
        s.LastUpdated.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
