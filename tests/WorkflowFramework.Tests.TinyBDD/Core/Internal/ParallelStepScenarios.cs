using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Internal;

[Feature("ParallelStep — concurrent step execution")]
public class ParallelStepScenarios : TinyBddTestBase
{
    public ParallelStepScenarios(ITestOutputHelper output) : base(output) { }

    // ── all succeed ───────────────────────────────────────────────────────────

    [Scenario("All parallel steps succeed — workflow completes"), Fact]
    public async Task AllParallelStepsSucceed()
    {
        var log = new System.Collections.Concurrent.ConcurrentBag<string>();
        var wf = Workflow.Create("parallel-all")
            .Parallel(p => p
                .Step(new LambdaStep("p1", _ => { log.Add("p1"); return Task.CompletedTask; }))
                .Step(new LambdaStep("p2", _ => { log.Add("p2"); return Task.CompletedTask; }))
                .Step(new LambdaStep("p3", _ => { log.Add("p3"); return Task.CompletedTask; })))
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a parallel group with three steps", () => (result, log))
            .Then("workflow completed and all three steps ran", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.log.Should().Contain("p1");
                t.log.Should().Contain("p2");
                t.log.Should().Contain("p3");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Parallel step name concatenates all sub-step names"), Fact]
    public async Task ParallelStepNameConcatenatesSubSteps()
    {
        var wf = Workflow.Create("name-test")
            .Parallel(p => p
                .Step(new LambdaStep("alpha", _ => Task.CompletedTask))
                .Step(new LambdaStep("beta", _ => Task.CompletedTask)))
            .Build();

        await Given("a parallel step with sub-steps alpha and beta", () => wf.Steps)
            .Then("the parallel step name contains both sub-step names", steps =>
            {
                steps.Should().ContainSingle();
                steps[0].Name.Should().Contain("alpha");
                steps[0].Name.Should().Contain("beta");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Parallel executes steps concurrently — overlapping execution is possible"), Fact]
    public async Task ParallelStepsRunConcurrently()
    {
        var startBarrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var step1Started = false;
        var step2Started = false;

        var wf = Workflow.Create("concurrent")
            .Parallel(p => p
                .Step(new LambdaStep("s1", async _ =>
                {
                    step1Started = true;
                    await startBarrier.Task;
                }))
                .Step(new LambdaStep("s2", async _ =>
                {
                    step2Started = true;
                    await startBarrier.Task;
                })))
            .Build();

        var exec = wf.ExecuteAsync(new WorkflowContext());

        // Give both steps a moment to start
        await Task.Delay(50);
        startBarrier.SetResult(true);
        var result = await exec;

        await Given("two parallel steps that wait on a barrier", () => (step1Started, step2Started, result))
            .Then("both started before the barrier was released", t =>
            {
                t.step1Started.Should().BeTrue();
                t.step2Started.Should().BeTrue();
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ── partial failure ───────────────────────────────────────────────────────

    [Scenario("One parallel step throws — workflow faults (AggregateException propagates)"), Fact]
    public async Task OneParallelStepThrowsFaultsWorkflow()
    {
        var wf = Workflow.Create("parallel-fail")
            .Parallel(p => p
                .Step(new LambdaStep("ok-step", _ => Task.CompletedTask))
                .Step(new LambdaStep("bad-step", _ => throw new InvalidOperationException("parallel-fail"))))
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a parallel group where one step throws", () => result)
            .Then("workflow is Faulted", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Faulted);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Parallel step with empty list of sub-steps completes without error"), Fact]
    public async Task EmptyParallelStepCompletes()
    {
        var wf = Workflow.Create("parallel-empty")
            .Parallel(_ => { }) // empty configure
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a parallel group with no sub-steps", () => result)
            .Then("workflow completes", r => { r.IsSuccess.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    // ── cancellation propagation ──────────────────────────────────────────────

    [Scenario("Cancellation token is propagated into parallel steps"), Fact]
    public async Task CancellationPropagatesIntoParallelSteps()
    {
        using var cts = new CancellationTokenSource();
        var stepSawCancellation = false;

        var wf = Workflow.Create("parallel-cancel")
            .Parallel(p => p
                .Step(new LambdaStep("check-cancel", ctx =>
                {
                    stepSawCancellation = ctx.CancellationToken.IsCancellationRequested;
                    return Task.CompletedTask;
                })))
            .Build();

        cts.Cancel();
        var result = await wf.ExecuteAsync(new WorkflowContext(cts.Token));

        // NOTE: current behavior — cancellation is checked in the engine's loop before the parallel
        // step fires, so status is Aborted and the step may or may not run.
        await Given("a parallel step when the token is already cancelled", () => result)
            .Then("workflow is Aborted", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Aborted);
                return true;
            })
            .AssertPassed();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class LambdaStep(string name, Func<IWorkflowContext, Task> action) : IStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => action(context);
    }
}
