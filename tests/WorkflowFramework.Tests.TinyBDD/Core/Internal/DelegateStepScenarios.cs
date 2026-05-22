using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Internal;

[Feature("DelegateStep — delegate-backed step execution")]
public class DelegateStepScenarios : TinyBddTestBase
{
    public DelegateStepScenarios(ITestOutputHelper output) : base(output) { }

    // ── sync / async delegates ────────────────────────────────────────────────

    [Scenario("Async delegate step executes and returns Task.CompletedTask"), Fact]
    public async Task AsyncDelegateStepExecutes()
    {
        var ran = false;
        var wf = Workflow.Create("async-delegate")
            .Step("my-step", _ => { ran = true; return Task.CompletedTask; })
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a step built from an async delegate", () => (result, ran))
            .Then("workflow completes and delegate ran", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.ran.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Delegate step name is set from the name argument"), Fact]
    public async Task DelegateStepNameIsPreserved()
    {
        var wf = Workflow.Create("name-check")
            .Step("explicit-name", _ => Task.CompletedTask)
            .Build();

        await Given("a delegate step with name 'explicit-name'", () => wf.Steps)
            .Then("Step.Name is 'explicit-name'", steps =>
            {
                steps[0].Name.Should().Be("explicit-name");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Delegate step can read and write context properties"), Fact]
    public async Task DelegateStepInteractsWithContext()
    {
        var wf = Workflow.Create("ctx-delegate")
            .Step("set-value", ctx => { ctx.Properties["answer"] = 42; return Task.CompletedTask; })
            .Build();

        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);

        await Given("a delegate that sets context.Properties['answer']=42", () => ctx.Properties)
            .Then("property is readable after execution", props =>
            {
                props["answer"].Should().Be(42);
                return true;
            })
            .AssertPassed();
    }

    // ── exception propagation ─────────────────────────────────────────────────

    [Scenario("Throwing delegate propagates exception as a workflow error"), Fact]
    public async Task ThrowingDelegatePropagatesException()
    {
        var wf = Workflow.Create("throw-delegate")
            .Step("thrower", _ => throw new InvalidOperationException("delegate-threw"))
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a step delegate that throws InvalidOperationException", () => result)
            .Then("workflow faults and exception is in errors", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Faulted);
                r.Errors.Should().ContainSingle();
                r.Errors[0].Exception.Should().BeOfType<InvalidOperationException>();
                r.Errors[0].StepName.Should().Be("thrower");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Delegate that throws after async work propagates the exception"), Fact]
    public async Task DelegateThatThrowsAfterAwaitPropagatesException()
    {
        var wf = Workflow.Create("async-throw")
            .Step("async-thrower", async _ =>
            {
                await Task.Yield();
                throw new ArgumentException("async-exception");
            })
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a delegate that throws after awaiting Task.Yield()", () => result)
            .Then("workflow faults with ArgumentException in errors", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Faulted);
                r.Errors[0].Exception.Should().BeOfType<ArgumentException>();
                return true;
            })
            .AssertPassed();
    }

    // ── null guards ───────────────────────────────────────────────────────────

    [Scenario("Step() with null action throws ArgumentNullException"), Fact]
    public async Task NullActionThrows()
    {
        Exception? caught = null;
        try { Workflow.Create("null-act").Step("name", (Func<IWorkflowContext, Task>)null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null delegate passed to Step(name, action)", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Step() with null name throws ArgumentNullException"), Fact]
    public async Task NullNameThrows()
    {
        Exception? caught = null;
        try { Workflow.Create("null-name").Step((string)null!, _ => Task.CompletedTask); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null name passed to Step(name, action)", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }
}
