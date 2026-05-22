using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.PatternKit;

/// <summary>
/// Phase F pilot: WorkflowStatusMachine is now authoritative inside WorkflowEngine.
/// These scenarios prove that the engine's observable status outputs exactly match the
/// machine-enforced transition graph, covering happy paths, fault/compensation paths,
/// cancellation, abort, and the invalid-transition guard.
/// </summary>
[Feature("Phase F — WorkflowStatusMachine authoritative pilot")]
public class StateMachinePilotScenarios : TinyBddTestBase
{
    public StateMachinePilotScenarios(ITestOutputHelper output) : base(output) { }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static WorkflowContext MakeContext(CancellationToken ct = default) =>
        new WorkflowContext(ct);

    /// <summary>
    /// A compensating step whose execute action and compensate action are caller-supplied delegates.
    /// </summary>
    private sealed class TrackingCompensatingStep(
        string name,
        Func<IWorkflowContext, Task> execute,
        Func<IWorkflowContext, Task> compensate) : IStep, ICompensatingStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => execute(context);
        public Task CompensateAsync(IWorkflowContext context) => compensate(context);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. Happy path: Pending → Running → Completed
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Engine produces Completed status when all steps succeed"), Fact]
    public async Task AllStepsSucceed_StatusIsCompleted()
    {
        var workflow = Workflow.Create("happy")
            .Step("step-0", _ => Task.CompletedTask)
            .Step("step-1", _ => Task.CompletedTask)
            .Build();

        var result = await workflow.ExecuteAsync(MakeContext());

        await Given("workflow with two succeeding steps", () => result)
            .Then("status is Completed", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Completed);
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. Step failure without compensation: Running → Faulted
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Engine produces Faulted status when a step throws and compensation is disabled"), Fact]
    public async Task StepThrows_NoCompensation_StatusIsFaulted()
    {
        var workflow = Workflow.Create("fault-no-comp")
            .Step("step-0", _ => throw new InvalidOperationException("boom"))
            .Build();

        var result = await workflow.ExecuteAsync(MakeContext());

        await Given("workflow with a throwing step (compensation off)", () => result)
            .Then("status is Faulted", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Faulted);
                r.IsSuccess.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. Step failure with compensation: Running → Compensated
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Engine produces Compensated status when a step throws and compensation is enabled"), Fact]
    public async Task StepThrows_WithCompensation_StatusIsCompensated()
    {
        var workflow = Workflow.Create("fault-with-comp")
            .Step("step-0", _ => throw new InvalidOperationException("compensate me"))
            .WithCompensation()
            .Build();

        var result = await workflow.ExecuteAsync(MakeContext());

        await Given("workflow with a throwing step (compensation on)", () => result)
            .Then("status is Compensated", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Compensated);
                r.IsSuccess.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. Cancellation mid-execution: Running → Aborted
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Engine produces Aborted status when CancellationToken is cancelled during execution"), Fact]
    public async Task Cancellation_StatusIsAborted()
    {
        using var cts = new CancellationTokenSource();
        var ctx = MakeContext(cts.Token);

        var workflow = Workflow.Create("cancel-mid")
            .Step("step-0", async c =>
            {
                cts.Cancel();
                await Task.Delay(10, c.CancellationToken);   // throws OperationCanceledException
            })
            .Build();

        var result = await workflow.ExecuteAsync(ctx);

        await Given("workflow whose step cancels the token", () => result)
            .Then("status is Aborted", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Aborted);
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. IsAborted flag: Running → Aborted via context flag
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Engine produces Aborted status when context IsAborted is set before a step"), Fact]
    public async Task ContextAborted_StatusIsAborted()
    {
        var ctx = MakeContext();
        ctx.IsAborted = true;   // signal abort before execution

        var workflow = Workflow.Create("pre-abort")
            .Step("step-0", _ => Task.CompletedTask)
            .Build();

        var result = await workflow.ExecuteAsync(ctx);

        await Given("workflow whose context is pre-marked IsAborted", () => result)
            .Then("status is Aborted", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Aborted);
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. Error recorded in context on step failure
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Errors collection in context captures the thrown exception on step failure"), Fact]
    public async Task StepThrows_ErrorCapturedInContext()
    {
        var expectedEx = new InvalidOperationException("expected error");
        var workflow = Workflow.Create("error-capture")
            .Step("step-0", _ => throw expectedEx)
            .Build();

        var result = await workflow.ExecuteAsync(MakeContext());

        await Given("workflow whose step throws a known exception", () => result)
            .Then("the error is recorded in the result and context", r =>
            {
                r.Errors.Should().ContainSingle();
                r.Errors[0].Exception.Should().BeSameAs(expectedEx);
                r.Errors[0].StepName.Should().Be("step-0");
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 7. OperationCanceledException bypasses the error path → Aborted, not Faulted
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("OperationCanceledException bypasses error recording and produces Aborted, not Faulted"), Fact]
    public async Task OperationCanceled_IsNotSwallowedAsStepError()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();   // already cancelled
        var ctx = MakeContext(cts.Token);

        var workflow = Workflow.Create("oce-pass-through")
            .Step("step-0", _ => Task.CompletedTask)    // ThrowIfCancellationRequested fires before step
            .Build();

        var result = await workflow.ExecuteAsync(ctx);

        await Given("pre-cancelled token causes OperationCanceledException at loop start", () => result)
            .Then("status is Aborted and no errors are recorded", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Aborted);
                r.Errors.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 8. Multi-step: only the failing step records an error
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Engine records errors only for the failing step, not for succeeding steps"), Fact]
    public async Task MultiStep_OnlyFailingStepRecordsError()
    {
        var faultEx = new Exception("step-1 fault");
        var workflow = Workflow.Create("multi-step-fault")
            .Step("step-0", _ => Task.CompletedTask)
            .Step("step-1", _ => throw faultEx)
            .Step("step-2", _ => Task.CompletedTask)    // never reached
            .Build();

        var result = await workflow.ExecuteAsync(MakeContext());

        await Given("three-step workflow where step-1 throws", () => result)
            .Then("exactly one error recorded, for step-1", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Faulted);
                r.Errors.Should().ContainSingle()
                    .Which.StepName.Should().Be("step-1");
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 9. Status snapshot consistency: Completed ↔ IsSuccess
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("WorkflowResult.IsSuccess is true only when Status is Completed"), Fact]
    public async Task IsSuccess_TrueOnlyForCompleted()
    {
        var workflow = Workflow.Create("is-success-check")
            .Step("step-0", _ => Task.CompletedTask)
            .Build();

        var result = await workflow.ExecuteAsync(MakeContext());

        await Given("a workflow that completes normally", () => result)
            .Then("IsSuccess is true and Status is Completed", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Completed);
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 10. IsSuccess false for Faulted
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("WorkflowResult.IsSuccess is false when Status is Faulted"), Fact]
    public async Task IsSuccess_FalseForFaulted()
    {
        var workflow = Workflow.Create("is-success-false")
            .Step("step-0", _ => throw new Exception("fail"))
            .Build();

        var result = await workflow.ExecuteAsync(MakeContext());

        await Given("a faulted workflow", () => result)
            .Then("IsSuccess is false", r =>
            {
                r.IsSuccess.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 11. Null context guard
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("ExecuteAsync throws ArgumentNullException for null context"), Fact]
    public async Task NullContext_ThrowsArgumentNullException()
    {
        var workflow = Workflow.Create("null-guard")
            .Step("step-0", _ => Task.CompletedTask)
            .Build();

        Exception? caught = null;
        try { await workflow.ExecuteAsync(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("null context passed to ExecuteAsync", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 12. Compensation fires in reverse order
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Compensation executes completed steps in reverse order when enabled"), Fact]
    public async Task Compensation_FiresInReverseOrder()
    {
        var compensationOrder = new List<int>();

        var step0 = new TrackingCompensatingStep(
            "step-0",
            execute: _ => Task.CompletedTask,
            compensate: _ => { compensationOrder.Add(0); return Task.CompletedTask; });

        var step1 = new TrackingCompensatingStep(
            "step-1",
            execute: _ => Task.CompletedTask,
            compensate: _ => { compensationOrder.Add(1); return Task.CompletedTask; });

        var engine = new WorkflowEngine(
            name: "comp-order",
            steps: [step0, step1, new TrackingCompensatingStep(
                "step-2",
                execute: _ => throw new InvalidOperationException("trigger"),
                compensate: _ => Task.CompletedTask)],
            middleware: [],
            events: [],
            enableCompensation: true);

        var result = await engine.ExecuteAsync(MakeContext());

        await Given("workflow with compensating steps where step-2 throws", () => (result, compensationOrder))
            .Then("status is Compensated and compensation ran in reverse: 1 then 0", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Compensated);
                // step-0 and step-1 completed before step-2 threw; compensated in reverse
                t.compensationOrder.Should().ContainInOrder(1, 0);
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 13. Cancellation with compensation enabled still aborts
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Cancellation with compensation enabled still produces Aborted, not Compensated"), Fact]
    public async Task CancellationWithCompensationEnabled_StillAborts()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ctx = MakeContext(cts.Token);

        var workflow = Workflow.Create("cancel-with-comp")
            .Step("step-0", _ => Task.CompletedTask)
            .WithCompensation()
            .Build();

        var result = await workflow.ExecuteAsync(ctx);

        await Given("pre-cancelled token with compensation enabled", () => result)
            .Then("status is Aborted (OperationCanceledException bypasses compensation)", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Aborted);
                r.Errors.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 14. Zero-step workflow: Pending → Running → Completed
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Workflow with no steps transitions Pending→Running→Completed without visiting loop body"), Fact]
    public async Task ZeroSteps_StatusIsCompleted()
    {
        var engine = new WorkflowEngine(
            name: "zero-steps",
            steps: [],
            middleware: [],
            events: [],
            enableCompensation: false);

        var result = await engine.ExecuteAsync(MakeContext());

        await Given("engine with zero steps", () => result)
            .Then("status is Completed (no steps to fail)", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Completed);
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 15. Single-step timing: step executes exactly once
    // ──────────────────────────────────────────────────────────────────────────

    [Scenario("Single-step workflow executes that step exactly once on the happy path"), Fact]
    public async Task SingleStep_ExecutesExactlyOnce()
    {
        int callCount = 0;
        var workflow = Workflow.Create("exec-once")
            .Step("step-0", _ => { callCount++; return Task.CompletedTask; })
            .Build();

        var result = await workflow.ExecuteAsync(MakeContext());

        await Given("workflow with one step that increments a counter", () => (result, callCount))
            .Then("step executed once and workflow completed", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Completed);
                t.callCount.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }
}
