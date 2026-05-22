using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Engine;

[Feature("WorkflowEngine — core execution loop")]
public class WorkflowEngineScenarios : TinyBddTestBase
{
    public WorkflowEngineScenarios(ITestOutputHelper output) : base(output) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IWorkflow BuildSingleStep(string stepName, Action<IWorkflowContext>? side = null)
    {
        return Workflow.Create("test")
            .Step(stepName, ctx =>
            {
                side?.Invoke(ctx);
                return Task.CompletedTask;
            })
            .Build();
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Scenario("Single-step workflow completes with Completed status"), Fact]
    public async Task SingleStepWorkflowCompletes()
    {
        var wf = BuildSingleStep("only-step");
        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a workflow with one step", () => result)
            .Then("status is Completed and IsSuccess is true", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Completed);
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multi-step workflow runs steps in order"), Fact]
    public async Task MultiStepWorkflowRunsInOrder()
    {
        var order = new List<string>();
        var wf = Workflow.Create("ordered")
            .Step("first",  _ => { order.Add("first");  return Task.CompletedTask; })
            .Step("second", _ => { order.Add("second"); return Task.CompletedTask; })
            .Step("third",  _ => { order.Add("third");  return Task.CompletedTask; })
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a three-step workflow", () => (result, order))
            .Then("all three steps ran in order and workflow completed", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.order.Should().Equal("first", "second", "third");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Workflow with no steps completes successfully"), Fact]
    public async Task WorkflowWithNoStepsCompletes()
    {
        var wf = Workflow.Create("empty").Build();
        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a workflow with no steps", () => result)
            .Then("status is Completed", r =>
            {
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Context properties propagate through all steps"), Fact]
    public async Task ContextPropertiesPropagateAcrossSteps()
    {
        var wf = Workflow.Create("ctx-test")
            .Step("write", ctx => { ctx.Properties["key"] = "hello"; return Task.CompletedTask; })
            .Step("read",  ctx => { ctx.Properties["read-back"] = ctx.Properties["key"]; return Task.CompletedTask; })
            .Build();

        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);

        await Given("a workflow where step 2 reads step 1's property", () => ctx.Properties)
            .Then("read-back equals the value written by step 1", props =>
            {
                props["read-back"].Should().Be("hello");
                return true;
            })
            .AssertPassed();
    }

    // ── failure path ──────────────────────────────────────────────────────────

    [Scenario("Step throws → workflow status is Faulted and error is recorded"), Fact]
    public async Task StepThrowsResultsInFaultedStatus()
    {
        var wf = Workflow.Create("fail-wf")
            .Step("bad-step", _ => throw new InvalidOperationException("boom"))
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a workflow whose step throws", () => result)
            .Then("status is Faulted and one error is captured", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Faulted);
                r.IsSuccess.Should().BeFalse();
                r.Errors.Should().ContainSingle();
                r.Errors[0].StepName.Should().Be("bad-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Steps after a failing step do not execute"), Fact]
    public async Task StepsAfterFailureDoNotRun()
    {
        var ran = new List<string>();
        var wf = Workflow.Create("partial-fail")
            .Step("step-a", _ => { ran.Add("step-a"); return Task.CompletedTask; })
            .Step("step-b", _ => throw new Exception("fail"))
            .Step("step-c", _ => { ran.Add("step-c"); return Task.CompletedTask; })
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("step-b throws after step-a succeeds", () => ran)
            .Then("only step-a ran; step-c was skipped", r =>
            {
                r.Should().ContainSingle("step-a");
                r.Should().NotContain("step-c");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("OperationCanceledException is not caught — status is Aborted"), Fact]
    public async Task OperationCanceledResultsInAborted()
    {
        using var cts = new CancellationTokenSource();
        var wf = Workflow.Create("cancel-wf")
            .Step("cancel-me", _ => { cts.Cancel(); return Task.FromCanceled(cts.Token); })
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext(cts.Token));

        await Given("a step that cancels the token and then raises OperationCanceledException", () => result)
            .Then("status is Aborted", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Aborted);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Pre-cancelled token aborts immediately — no steps run"), Fact]
    public async Task PreCancelledTokenAbortsImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ran = false;
        var wf = Workflow.Create("pre-cancel")
            .Step("should-not-run", _ => { ran = true; return Task.CompletedTask; })
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext(cts.Token));

        await Given("workflow started with an already-cancelled token", () => (result, ran))
            .Then("status is Aborted and the step did not run", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Aborted);
                t.ran.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IsAborted flag stops execution mid-workflow"), Fact]
    public async Task IsAbortedFlagStopsExecution()
    {
        var ran = new List<string>();
        var wf = Workflow.Create("abort-flag")
            .Step("step-a", ctx => { ran.Add("step-a"); ctx.IsAborted = true; return Task.CompletedTask; })
            .Step("step-b", _ => { ran.Add("step-b"); return Task.CompletedTask; })
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("step-a sets IsAborted=true", () => (result, ran))
            .Then("status is Aborted and step-b did not run", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Aborted);
                t.ran.Should().NotContain("step-b");
                return true;
            })
            .AssertPassed();
    }

    // ── compensation ──────────────────────────────────────────────────────────

    [Scenario("Compensation is triggered in reverse order when a step fails"), Fact]
    public async Task CompensationRunsInReverseOrder()
    {
        var log = new List<string>();

        var wf = Workflow.Create("saga")
            .WithCompensation()
            .Step(new CompensatableStep("step-a", log))
            .Step(new CompensatableStep("step-b", log))
            .Step("fail-step", _ => throw new InvalidOperationException("saga-fail"))
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a saga where step-3 throws after steps a and b succeed", () => (result, log))
            .Then("status is Compensated and compensation ran in reverse", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Compensated);
                t.log.Should().Contain("compensate:step-b");
                t.log.Should().Contain("compensate:step-a");
                // Reverse order: b before a
                t.log.IndexOf("compensate:step-b").Should().BeLessThan(t.log.IndexOf("compensate:step-a"));
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Without WithCompensation, a step failure results in Faulted not Compensated"), Fact]
    public async Task WithoutCompensationFlagResultIsFaulted()
    {
        var wf = Workflow.Create("no-saga")
            .Step(new CompensatableStep("step-a", new List<string>()))
            .Step("bad", _ => throw new Exception("fail"))
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a workflow without compensation where a step throws", () => result)
            .Then("status is Faulted (not Compensated)", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Faulted);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Compensation errors are swallowed so all compensating steps run"), Fact]
    public async Task CompensationErrorsSwallowed()
    {
        var log = new List<string>();
        var wf = Workflow.Create("swallow-saga")
            .WithCompensation()
            .Step(new CompensatableStep("step-a", log))
            .Step(new ThrowingCompensatableStep("step-b"))
            .Step("fail-step", _ => throw new Exception("trigger"))
            .Build();

        WorkflowResult result = null!;
        var act = async () => result = await wf.ExecuteAsync(new WorkflowContext());

        await act.Should().NotThrowAsync();

        await Given("saga where compensation of step-b throws", () => (result, log))
            .Then("status is Compensated and step-a still compensated", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Compensated);
                t.log.Should().Contain("compensate:step-a");
                return true;
            })
            .AssertPassed();
    }

    // ── event emission ────────────────────────────────────────────────────────

    [Scenario("Events are raised in the correct lifecycle order for a successful workflow"), Fact]
    public async Task EventsRaisedInCorrectOrder()
    {
        var events = new List<string>();
        var recorder = new RecordingEvents(events);
        var wf = Workflow.Create("event-wf")
            .WithEvents(recorder)
            .Step("s1", _ => Task.CompletedTask)
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a one-step workflow with an event recorder attached", () => events)
            .Then("events fire in order: started → step-started → step-completed → completed", e =>
            {
                e.Should().Equal("workflow:started", "step:started:s1", "step:completed:s1", "workflow:completed");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("OnStepFailed and OnWorkflowFailed fire when a step throws"), Fact]
    public async Task FailureEventsFireOnStepException()
    {
        var events = new List<string>();
        var recorder = new RecordingEvents(events);
        var wf = Workflow.Create("fail-events")
            .WithEvents(recorder)
            .Step("boom", _ => throw new Exception("err"))
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a workflow whose step throws with an event recorder", () => events)
            .Then("step-failed and workflow-failed events are recorded", e =>
            {
                e.Should().Contain("step:failed:boom");
                e.Should().Contain("workflow:failed");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple event handlers all receive notifications"), Fact]
    public async Task MultipleEventHandlersAllInvoked()
    {
        var log1 = new List<string>();
        var log2 = new List<string>();
        var wf = Workflow.Create("multi-events")
            .WithEvents(new RecordingEvents(log1))
            .WithEvents(new RecordingEvents(log2))
            .Step("step", _ => Task.CompletedTask)
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("two event handlers attached", () => (log1, log2))
            .Then("both receive the completed event", t =>
            {
                t.log1.Should().Contain("workflow:completed");
                t.log2.Should().Contain("workflow:completed");
                return true;
            })
            .AssertPassed();
    }

    // ── middleware ordering ───────────────────────────────────────────────────

    [Scenario("Middleware wraps steps in registration order (outer-to-inner)"), Fact]
    public async Task MiddlewareWrapsStepsOuterToInner()
    {
        var log = new List<string>();
        var wf = Workflow.Create("mw-order")
            .Use(new OrderingMiddleware("first", log))
            .Use(new OrderingMiddleware("second", log))
            .Step("step", _ => { log.Add("step"); return Task.CompletedTask; })
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("two middlewares registered in order first→second", () => log)
            .Then("execution order is first-before, second-before, step, second-after, first-after", l =>
            {
                l.Should().Equal("first:before", "second:before", "step", "second:after", "first:after");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Middleware can short-circuit step execution"), Fact]
    public async Task MiddlewareCanShortCircuit()
    {
        var stepRan = false;
        var wf = Workflow.Create("short-circuit")
            .Use(new ShortCircuitMiddleware())
            .Step("step", _ => { stepRan = true; return Task.CompletedTask; })
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("middleware that does not call next()", () => (result, stepRan))
            .Then("the step did not run and the workflow completed", t =>
            {
                t.stepRan.Should().BeFalse();
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Workflow name is exposed on the IWorkflow interface"), Fact]
    public async Task WorkflowNameIsExposed()
    {
        var wf = Workflow.Create("my-workflow").Build();

        await Given("a workflow with name 'my-workflow'", () => wf.Name)
            .Then("the Name property returns 'my-workflow'", name =>
            {
                name.Should().Be("my-workflow");
                return true;
            })
            .AssertPassed();
    }

    // ── null guards ───────────────────────────────────────────────────────────

    [Scenario("ExecuteAsync throws ArgumentNullException for null context"), Fact]
    public async Task ExecuteAsyncThrowsForNullContext()
    {
        var wf = Workflow.Create("null-guard").Build();
        Exception? caught = null;
        try { await wf.ExecuteAsync(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null passed as context", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ── step index tracking ───────────────────────────────────────────────────

    [Scenario("CurrentStepIndex and CurrentStepName are updated during execution"), Fact]
    public async Task CurrentStepIndexAndNameAreUpdated()
    {
        var capturedIndex = -1;
        var capturedName = string.Empty;
        var wf = Workflow.Create("step-tracking")
            .Step("capture-step", ctx =>
            {
                capturedIndex = ctx.CurrentStepIndex;
                capturedName = ctx.CurrentStepName ?? string.Empty;
                return Task.CompletedTask;
            })
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a workflow whose step reads its own index and name from context", () => (capturedIndex, capturedName))
            .Then("index is 0 and name is 'capture-step'", t =>
            {
                t.capturedIndex.Should().Be(0);
                t.capturedName.Should().Be("capture-step");
                return true;
            })
            .AssertPassed();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class CompensatableStep(string name, List<string> log) : ICompensatingStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) { log.Add($"execute:{Name}"); return Task.CompletedTask; }
        public Task CompensateAsync(IWorkflowContext context) { log.Add($"compensate:{Name}"); return Task.CompletedTask; }
    }

    private sealed class ThrowingCompensatableStep(string name) : ICompensatingStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
        public Task CompensateAsync(IWorkflowContext context) => throw new InvalidOperationException("compensation failed");
    }

    private sealed class RecordingEvents(List<string> log) : WorkflowEventsBase
    {
        public override Task OnWorkflowStartedAsync(IWorkflowContext ctx) { log.Add("workflow:started"); return Task.CompletedTask; }
        public override Task OnWorkflowCompletedAsync(IWorkflowContext ctx) { log.Add("workflow:completed"); return Task.CompletedTask; }
        public override Task OnWorkflowFailedAsync(IWorkflowContext ctx, Exception ex) { log.Add("workflow:failed"); return Task.CompletedTask; }
        public override Task OnStepStartedAsync(IWorkflowContext ctx, IStep step) { log.Add($"step:started:{step.Name}"); return Task.CompletedTask; }
        public override Task OnStepCompletedAsync(IWorkflowContext ctx, IStep step) { log.Add($"step:completed:{step.Name}"); return Task.CompletedTask; }
        public override Task OnStepFailedAsync(IWorkflowContext ctx, IStep step, Exception ex) { log.Add($"step:failed:{step.Name}"); return Task.CompletedTask; }
    }

    private sealed class OrderingMiddleware(string name, List<string> log) : IWorkflowMiddleware
    {
        public async Task InvokeAsync(IWorkflowContext ctx, IStep step, StepDelegate next)
        {
            log.Add($"{name}:before");
            await next(ctx);
            log.Add($"{name}:after");
        }
    }

    private sealed class ShortCircuitMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext ctx, IStep step, StepDelegate next) => Task.CompletedTask;
    }
}
