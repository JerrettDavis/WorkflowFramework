using System.Diagnostics;
using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Diagnostics;

public class TracingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithoutListener_DoesNotThrow()
    {
        var mw = new TracingMiddleware();
        await mw.InvokeAsync(Ctx(), Step("S"), _ => Task.CompletedTask);
    }

    [Fact]
    public async Task InvokeAsync_WithListener_SetsOkStatus()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == WorkflowActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);
        await new TracingMiddleware().InvokeAsync(Ctx(), Step("OkStep_Unique"), _ => Task.CompletedTask);
        var captured = activities.FirstOrDefault(a => a.DisplayName.Contains("OkStep_Unique"));
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task InvokeAsync_OnError_SetsErrorStatusAndEvent()
    {
        Activity? captured = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == WorkflowActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => captured = a
        };
        ActivitySource.AddActivityListener(listener);
        await Assert.ThrowsAsync<Exception>(() =>
            new TracingMiddleware().InvokeAsync(Ctx(), Step("Fail"), _ => throw new Exception("err")));
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(ActivityStatusCode.Error);
        captured.Events.Should().Contain(e => e.Name == "exception");
    }

    private static IWorkflowContext Ctx() => new C();
    private static IStep Step(string n) => new St(n);
    private class St(string n) : IStep
    {
        public string Name { get; } = n;
        public Task ExecuteAsync(IWorkflowContext c) => Task.CompletedTask; }
    private class C : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}

public class TimingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RecordsTiming()
    {
        var mw = new TimingMiddleware();
        var ctx = Ctx();
        await mw.InvokeAsync(ctx, Step("S1"), _ => Task.CompletedTask);
        ctx.Properties.Should().ContainKey(TimingMiddleware.TimingsKey);
        var timings = (Dictionary<string, TimeSpan>)ctx.Properties[TimingMiddleware.TimingsKey]!;
        timings.Should().ContainKey("S1");
    }

    [Fact]
    public async Task InvokeAsync_MultipleSteps_RecordsAll()
    {
        var mw = new TimingMiddleware();
        var ctx = Ctx();
        await mw.InvokeAsync(ctx, Step("A"), _ => Task.CompletedTask);
        await mw.InvokeAsync(ctx, Step("B"), _ => Task.CompletedTask);
        var timings = (Dictionary<string, TimeSpan>)ctx.Properties[TimingMiddleware.TimingsKey]!;
        timings.Should().HaveCount(2);
    }

    [Fact]
    public async Task InvokeAsync_OnError_StillRecordsTiming()
    {
        var mw = new TimingMiddleware();
        var ctx = Ctx();
        try { await mw.InvokeAsync(ctx, Step("F"), _ => throw new Exception()); } catch { }
        var timings = (Dictionary<string, TimeSpan>)ctx.Properties[TimingMiddleware.TimingsKey]!;
        timings.Should().ContainKey("F");
    }

    [Fact]
    public void TimingsKey_IsSet()
    {
        TimingMiddleware.TimingsKey.Should().NotBeNullOrEmpty();
    }

    private static C Ctx() => new();
    private static IStep Step(string n) => new St(n);
    private class St(string n) : IStep
    {
        public string Name { get; } = n;
        public Task ExecuteAsync(IWorkflowContext c) => Task.CompletedTask; }
    private class C : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}

public class WorkflowActivitySourceTests
{
    [Fact]
    public void Name_IsWorkflowFramework()
    {
        WorkflowActivitySource.Name.Should().Be("WorkflowFramework");
    }

    [Fact]
    public void Instance_IsNotNull()
    {
        WorkflowActivitySource.Instance.Should().NotBeNull();
    }

    [Fact]
    public void Instance_HasCorrectName()
    {
        WorkflowActivitySource.Instance.Name.Should().Be("WorkflowFramework");
    }
}
