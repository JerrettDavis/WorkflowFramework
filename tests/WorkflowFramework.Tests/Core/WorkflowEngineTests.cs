using FluentAssertions;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowEngineTests
{
    [Fact]
    public void Constructor_NullName_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowEngine(null!, Array.Empty<IStep>(), Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_NullSteps_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowEngine("test", null!, Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        act.Should().Throw<ArgumentNullException>().WithParameterName("steps");
    }

    [Fact]
    public void Constructor_NullMiddleware_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowEngine("test", Array.Empty<IStep>(), null!, Array.Empty<IWorkflowEvents>(), false);
        act.Should().Throw<ArgumentNullException>().WithParameterName("middleware");
    }

    [Fact]
    public void Constructor_NullEvents_ThrowsArgumentNullException()
    {
        var act = () => new WorkflowEngine("test", Array.Empty<IStep>(), Array.Empty<IWorkflowMiddleware>(), null!, false);
        act.Should().Throw<ArgumentNullException>().WithParameterName("events");
    }

    [Fact]
    public void Name_ReturnsConstructorValue()
    {
        var engine = new WorkflowEngine("MyWorkflow", Array.Empty<IStep>(), Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        engine.Name.Should().Be("MyWorkflow");
    }

    [Fact]
    public void Steps_ReturnsProvidedSteps()
    {
        var steps = new IStep[] { new TrackingStep("A"), new TrackingStep("B") };
        var engine = new WorkflowEngine("test", steps, Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        engine.Steps.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_NullContext_ThrowsArgumentNullException()
    {
        var engine = new WorkflowEngine("test", Array.Empty<IStep>(), Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        var act = () => engine.ExecuteAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task ExecuteAsync_NoSteps_ReturnsCompleted()
    {
        var engine = new WorkflowEngine("test", Array.Empty<IStep>(), Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        var result = await engine.ExecuteAsync(new WorkflowContext());
        result.Status.Should().Be(WorkflowStatus.Completed);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSteps_ExecutesInOrder()
    {
        var steps = new IStep[] { new TrackingStep("A"), new TrackingStep("B"), new TrackingStep("C") };
        var engine = new WorkflowEngine("test", steps, Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        var context = new WorkflowContext();
        await engine.ExecuteAsync(context);
        TrackingStep.GetLog(context).Should().ContainInOrder("A", "B", "C");
    }

    [Fact]
    public async Task ExecuteAsync_SetsCurrentStepNameAndIndex()
    {
        string? capturedName = null;
        int capturedIndex = -1;
        var step = new DelegateTestStep("capture", ctx =>
        {
            capturedName = ctx.CurrentStepName;
            capturedIndex = ctx.CurrentStepIndex;
            return Task.CompletedTask;
        });
        var engine = new WorkflowEngine("test", new IStep[] { new TrackingStep("First"), step }, Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        await engine.ExecuteAsync(new WorkflowContext());
        capturedName.Should().Be("capture");
        capturedIndex.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_StepThrows_ReturnsFaulted()
    {
        var engine = new WorkflowEngine("test", new IStep[] { new FailingStep() }, Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        var result = await engine.ExecuteAsync(new WorkflowContext());
        result.Status.Should().Be(WorkflowStatus.Faulted);
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].StepName.Should().Be("FailingStep");
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ReturnsAborted()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var engine = new WorkflowEngine("test", new IStep[] { new TrackingStep("A") }, Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        var result = await engine.ExecuteAsync(new WorkflowContext(cts.Token));
        result.Status.Should().Be(WorkflowStatus.Aborted);
    }

    [Fact]
    public async Task ExecuteAsync_IsAborted_ReturnsAborted()
    {
        var context = new WorkflowContext { IsAborted = true };
        var engine = new WorkflowEngine("test", new IStep[] { new TrackingStep("A") }, Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), false);
        var result = await engine.ExecuteAsync(context);
        result.Status.Should().Be(WorkflowStatus.Aborted);
    }

    [Fact]
    public async Task ExecuteAsync_WithCompensation_StepFails_ReturnsCompensated()
    {
        var steps = new IStep[]
        {
            new CompensatingTrackingStep("S1"),
            new CompensatingTrackingStep("S2"),
            new FailingStep()
        };
        var engine = new WorkflowEngine("test", steps, Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), true);
        var context = new WorkflowContext();
        var result = await engine.ExecuteAsync(context);
        result.Status.Should().Be(WorkflowStatus.Compensated);
        var log = TrackingStep.GetLog(context);
        log.Should().Contain("S2:Compensate");
        log.Should().Contain("S1:Compensate");
        // Compensation is in reverse order
        log.IndexOf("S2:Compensate").Should().BeLessThan(log.IndexOf("S1:Compensate"));
    }

    [Fact]
    public async Task ExecuteAsync_WithMiddleware_MiddlewareWrapsExecution()
    {
        var log = new List<string>();
        var middleware = new TestMiddleware(log);
        var steps = new IStep[] { new TrackingStep("A") };
        var engine = new WorkflowEngine("test", steps, new IWorkflowMiddleware[] { middleware }, Array.Empty<IWorkflowEvents>(), false);
        var context = new WorkflowContext();
        await engine.ExecuteAsync(context);
        log.Should().ContainInOrder("Before", "After");
    }

    [Fact]
    public async Task ExecuteAsync_WithEvents_RaisesAllEvents()
    {
        var events = new TestEvents();
        var engine = new WorkflowEngine("test", new IStep[] { new TrackingStep("A") }, Array.Empty<IWorkflowMiddleware>(), new IWorkflowEvents[] { events }, false);
        await engine.ExecuteAsync(new WorkflowContext());
        events.Log.Should().ContainInOrder("WorkflowStarted", "StepStarted:A", "StepCompleted:A", "WorkflowCompleted");
    }

    [Fact]
    public async Task ExecuteAsync_StepFails_RaisesFailedEvents()
    {
        var events = new TestEvents();
        var engine = new WorkflowEngine("test", new IStep[] { new FailingStep() }, Array.Empty<IWorkflowMiddleware>(), new IWorkflowEvents[] { events }, false);
        await engine.ExecuteAsync(new WorkflowContext());
        events.Log.Should().Contain("StepFailed:FailingStep");
        events.Log.Should().Contain("WorkflowFailed");
    }

    [Fact]
    public async Task ExecuteAsync_MiddlewareShortCircuits_StepNotExecuted()
    {
        var middleware = new ShortCircuitMiddleware();
        var steps = new IStep[] { new TrackingStep("A") };
        var engine = new WorkflowEngine("test", steps, new IWorkflowMiddleware[] { middleware }, Array.Empty<IWorkflowEvents>(), false);
        var context = new WorkflowContext();
        await engine.ExecuteAsync(context);
        TrackingStep.GetLog(context).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleMiddleware_ExecutedInOrder()
    {
        var log = new List<string>();
        var m1 = new OrderedMiddleware("M1", log);
        var m2 = new OrderedMiddleware("M2", log);
        var engine = new WorkflowEngine("test", new IStep[] { new TrackingStep("A") }, new IWorkflowMiddleware[] { m1, m2 }, Array.Empty<IWorkflowEvents>(), false);
        await engine.ExecuteAsync(new WorkflowContext());
        log.Should().ContainInOrder("M1:Before", "M2:Before", "M2:After", "M1:After");
    }

    [Fact]
    public async Task ExecuteAsync_CompensationErrors_SwallowedAndAllCompensated()
    {
        var steps = new IStep[]
        {
            new FailingCompensatingStep("S1"),
            new CompensatingTrackingStep("S2"),
            new FailingStep()
        };
        var engine = new WorkflowEngine("test", steps, Array.Empty<IWorkflowMiddleware>(), Array.Empty<IWorkflowEvents>(), true);
        var context = new WorkflowContext();
        var result = await engine.ExecuteAsync(context);
        result.Status.Should().Be(WorkflowStatus.Compensated);
        // S2 should still have compensated even though S1 compensation throws
        TrackingStep.GetLog(context).Should().Contain("S2:Compensate");
    }

    // Helper classes
    private class DelegateTestStep : IStep
    {
        private readonly Func<IWorkflowContext, Task> _action;
        public DelegateTestStep(string name, Func<IWorkflowContext, Task> action) { Name = name; _action = action; }
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => _action(context);
    }

    private class TestMiddleware : IWorkflowMiddleware
    {
        private readonly List<string> _log;
        public TestMiddleware(List<string> log) => _log = log;
        public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
        {
            _log.Add("Before");
            await next(context);
            _log.Add("After");
        }
    }

    private class ShortCircuitMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => Task.CompletedTask;
    }

    private class OrderedMiddleware : IWorkflowMiddleware
    {
        private readonly string _name;
        private readonly List<string> _log;
        public OrderedMiddleware(string name, List<string> log) { _name = name; _log = log; }
        public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
        {
            _log.Add($"{_name}:Before");
            await next(context);
            _log.Add($"{_name}:After");
        }
    }

    private class TestEvents : WorkflowEventsBase
    {
        public List<string> Log { get; } = new();
        public override Task OnWorkflowStartedAsync(IWorkflowContext context) { Log.Add("WorkflowStarted"); return Task.CompletedTask; }
        public override Task OnWorkflowCompletedAsync(IWorkflowContext context) { Log.Add("WorkflowCompleted"); return Task.CompletedTask; }
        public override Task OnWorkflowFailedAsync(IWorkflowContext context, Exception ex) { Log.Add("WorkflowFailed"); return Task.CompletedTask; }
        public override Task OnStepStartedAsync(IWorkflowContext context, IStep step) { Log.Add($"StepStarted:{step.Name}"); return Task.CompletedTask; }
        public override Task OnStepCompletedAsync(IWorkflowContext context, IStep step) { Log.Add($"StepCompleted:{step.Name}"); return Task.CompletedTask; }
        public override Task OnStepFailedAsync(IWorkflowContext context, IStep step, Exception ex) { Log.Add($"StepFailed:{step.Name}"); return Task.CompletedTask; }
    }

    private class FailingCompensatingStep : ICompensatingStep
    {
        public FailingCompensatingStep(string name) => Name = name;
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) { TrackingStep.GetLog(context).Add($"{Name}:Execute"); return Task.CompletedTask; }
        public Task CompensateAsync(IWorkflowContext context) => throw new InvalidOperationException("Compensation failed");
    }
}
