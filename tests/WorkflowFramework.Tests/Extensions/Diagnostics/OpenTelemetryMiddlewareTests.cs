using System.Diagnostics;
using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Diagnostics;

public class OpenTelemetryMiddlewareTests
{
    private readonly OpenTelemetryMiddleware _middleware = new();

    [Fact]
    public async Task InvokeAsync_DoesNotThrow_WithoutListener()
    {
        var ctx = CreateCtx();
        var step = new TestStep("S1");
        await _middleware.InvokeAsync(ctx, step, _ => Task.CompletedTask);
    }

    [Fact]
    public async Task InvokeAsync_WithListener_CreatesSpan()
    {
        // Use a unique step name to avoid capturing activities from parallel tests
        var stepName = $"CreatesSpan_{Guid.NewGuid():N}";
        Activity? captured = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == WorkflowActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a =>
            {
                if (a.OperationName == $"Step:{stepName}")
                    captured = a;
            }
        };
        ActivitySource.AddActivityListener(listener);

        var ctx = CreateCtx();
        await _middleware.InvokeAsync(ctx, new TestStep(stepName), _ => Task.CompletedTask);

        captured.Should().NotBeNull();
        captured!.GetTagItem("workflow.step.name").Should().Be(stepName);
        captured.GetTagItem("workflow.step.status").Should().Be("completed");
    }

    [Fact]
    public async Task InvokeAsync_OnError_SetsErrorStatus()
    {
        // Use a unique step name to avoid capturing activities from parallel tests
        var stepName = $"FailError_{Guid.NewGuid():N}";
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == WorkflowActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a =>
            {
                if (a.OperationName == $"Step:{stepName}")
                    activities.Add(a);
            }
        };
        ActivitySource.AddActivityListener(listener);

        var ctx = CreateCtx();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _middleware.InvokeAsync(ctx, new TestStep(stepName), _ => throw new InvalidOperationException("boom")));

        activities.Should().ContainSingle();
        var captured = activities[0];
        captured.Status.Should().Be(ActivityStatusCode.Error);
        captured.GetTagItem("workflow.step.status").Should().Be("failed");
    }

    private static TestCtx CreateCtx() => new();
    private class TestStep(string n) : IStep
    {
        public string Name { get; } = n;
        public Task ExecuteAsync(IWorkflowContext c) => Task.CompletedTask; }
    private class TestCtx : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}
