using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Builder;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Internal;

[Feature("LoopSteps — ForEach, While, DoWhile, RetryGroup, TryCatch loop semantics")]
public class LoopStepsScenarios : TinyBddTestBase
{
    public LoopStepsScenarios(ITestOutputHelper output) : base(output) { }

    // ── ForEachStep ────────────────────────────────────────────────────────────

    [Scenario("ForEach iterates over three items and body runs for each"), Fact]
    public async Task ForEachIteratesThreeItems()
    {
        var collected = new List<string>();
        var wf = Workflow.Create("fe3")
            .ForEach(
                _ => new[] { "x", "y", "z" },
                b => b.Step("collect", ctx =>
                {
                    collected.Add((string)ctx.Properties["ForEach.Current"]!);
                    return Task.CompletedTask;
                }))
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("ForEach over [x,y,z]", () => collected)
            .Then("collected list equals [x,y,z]", c => { c.Should().Equal("x", "y", "z"); return true; })
            .AssertPassed();
    }

    [Scenario("ForEach over empty collection — body never runs"), Fact]
    public async Task ForEachOverEmptyCollectionSkipsBody()
    {
        var ran = false;
        var wf = Workflow.Create("fe-empty")
            .ForEach(
                _ => Enumerable.Empty<int>(),
                b => b.Step("body", _ => { ran = true; return Task.CompletedTask; }))
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("ForEach over an empty sequence", () => ran)
            .Then("body never ran", r => { r.Should().BeFalse(); return true; })
            .AssertPassed();
    }

    [Scenario("ForEach stops iteration when IsAborted is set"), Fact]
    public async Task ForEachStopsWhenAborted()
    {
        var count = 0;
        var log = new List<string>();
        var wf = Workflow.Create("fe-abort")
            .ForEach(
                _ => new[] { 1, 2, 3, 4, 5 },
                b => b.Step("abort-after-2", ctx =>
                {
                    count++;
                    if (count >= 2) ctx.IsAborted = true;
                    return Task.CompletedTask;
                }))
            .Step("after-foreach", _ => { log.Add("after"); return Task.CompletedTask; })
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        // NOTE: current behavior — IsAborted inside ForEach stops loop iteration;
        // the engine then checks IsAborted before the NEXT step and returns Aborted.
        await Given("ForEach that aborts after 2 items with a subsequent step", () => (result, count, log))
            .Then("iteration stopped early, workflow Aborted, subsequent step skipped", t =>
            {
                t.count.Should().BeLessThanOrEqualTo(2);
                t.result.Status.Should().Be(WorkflowStatus.Aborted);
                t.log.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    // ── WhileStep ─────────────────────────────────────────────────────────────

    [Scenario("While runs body exactly 5 times for a counter-based condition"), Fact]
    public async Task WhileRunsBodyFiveTimes()
    {
        var count = 0;
        var wf = Workflow.Create("while-5")
            .While(
                _ => count < 5,
                b => b.Step("inc", _ => { count++; return Task.CompletedTask; }))
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("While loop until count=5", () => count)
            .Then("count is 5", c => { c.Should().Be(5); return true; })
            .AssertPassed();
    }

    [Scenario("While with initially-false condition never runs body"), Fact]
    public async Task WhileWithFalseConditionSkipsBody()
    {
        var ran = false;
        var wf = Workflow.Create("while-false")
            .While(_ => false, b => b.Step("body", _ => { ran = true; return Task.CompletedTask; }))
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a While loop with condition=false", () => ran)
            .Then("body never ran", r => { r.Should().BeFalse(); return true; })
            .AssertPassed();
    }

    [Scenario("While stops when IsAborted is set inside the loop body"), Fact]
    public async Task WhileStopsWhenAborted()
    {
        var count = 0;
        var afterRan = false;
        var wf = Workflow.Create("while-abort")
            .While(
                _ => true,
                b => b.Step("body", ctx =>
                {
                    count++;
                    ctx.IsAborted = true;
                    return Task.CompletedTask;
                }))
            .Step("after-while", _ => { afterRan = true; return Task.CompletedTask; })
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        // NOTE: current behavior — IsAborted inside While stops loop iteration;
        // the engine detects IsAborted before the NEXT step and returns Aborted.
        await Given("a While loop that aborts after first iteration (with a subsequent step)", () => (result, count, afterRan))
            .Then("body ran once, status is Aborted, subsequent step skipped", t =>
            {
                t.count.Should().Be(1);
                t.result.Status.Should().Be(WorkflowStatus.Aborted);
                t.afterRan.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    // ── DoWhileStep ───────────────────────────────────────────────────────────

    [Scenario("DoWhile runs body once even when condition is immediately false"), Fact]
    public async Task DoWhileRunsAtLeastOnce()
    {
        var count = 0;
        var wf = Workflow.Create("dw-once")
            .DoWhile(
                b => b.Step("body", _ => { count++; return Task.CompletedTask; }),
                _ => false)
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a DoWhile whose post-condition is always false", () => count)
            .Then("body ran exactly once", c => { c.Should().Be(1); return true; })
            .AssertPassed();
    }

    [Scenario("DoWhile runs body three times for count < 3 condition"), Fact]
    public async Task DoWhileRunsThreeTimes()
    {
        var count = 0;
        var wf = Workflow.Create("dw-3")
            .DoWhile(
                b => b.Step("inc", _ => { count++; return Task.CompletedTask; }),
                _ => count < 3)
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a DoWhile that repeats while count < 3", () => count)
            .Then("count is 3", c => { c.Should().Be(3); return true; })
            .AssertPassed();
    }

    // ── RetryGroupStep ────────────────────────────────────────────────────────

    [Scenario("Retry succeeds on the second attempt after one transient failure"), Fact]
    public async Task RetrySucceedsOnSecondAttempt()
    {
        var attempts = 0;
        var wf = Workflow.Create("retry-2")
            .Retry(
                b => b.Step("flaky", _ =>
                {
                    attempts++;
                    if (attempts == 1) throw new Exception("transient");
                    return Task.CompletedTask;
                }),
                maxAttempts: 3)
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a Retry group that fails once then succeeds", () => (result, attempts))
            .Then("workflow succeeds after 2 attempts", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.attempts.Should().Be(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Retry exhausts all attempts and propagates the last exception"), Fact]
    public async Task RetryExhaustsAndFaults()
    {
        var attempts = 0;
        var wf = Workflow.Create("retry-exhaust")
            .Retry(
                b => b.Step("always-fail", _ => { attempts++; throw new Exception("persistent"); }),
                maxAttempts: 3)
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a Retry group that always fails with maxAttempts=3", () => (result, attempts))
            .Then("workflow faults after 3 attempts", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Faulted);
                t.attempts.Should().Be(3);
                return true;
            })
            .AssertPassed();
    }

    // ── TryCatchStep ─────────────────────────────────────────────────────────

    [Scenario("TryCatch catches base-class exceptions via polymorphic lookup"), Fact]
    public async Task TryCatchCatchesBaseClassException()
    {
        var caught = false;
        var wf = Workflow.Create("base-catch")
            .Try(b => b.Step("throw", _ => throw new ArgumentNullException("arg")))
            .Catch<ArgumentException>((_, _) => { caught = true; return Task.CompletedTask; })
            .EndTry()
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("TryCatch catching ArgumentException when ArgumentNullException is thrown", () => (result, caught))
            .Then("caught is true because ArgumentNullException derives from ArgumentException", t =>
            {
                t.caught.Should().BeTrue();
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TryCatch Finally block runs even when no exception is thrown"), Fact]
    public async Task TryCatchFinallyRunsWithoutException()
    {
        var finallyRan = false;
        var wf = Workflow.Create("finally-no-ex")
            .Try(b => b.Step("success", _ => Task.CompletedTask))
            .Catch<Exception>((_, _) => Task.CompletedTask)
            .Finally(fin => fin.Step("finally", _ => { finallyRan = true; return Task.CompletedTask; }))
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("TryCatch with finally when no exception is thrown", () => finallyRan)
            .Then("finally block still ran", f => { f.Should().BeTrue(); return true; })
            .AssertPassed();
    }
}
