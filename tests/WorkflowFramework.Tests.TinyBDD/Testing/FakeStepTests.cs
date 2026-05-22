using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Testing;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Testing;

[Feature("FakeStep and FakeStep<T> behaviour")]
public class FakeStepTests : TinyBddTestBase
{
    public FakeStepTests(ITestOutputHelper output) : base(output) { }

    [Scenario("FakeStep.ExecutionCount increments each invocation"), Fact]
    public async Task ExecutionCountIncrements()
    {
        var step = new FakeStep("counter-step");
        var ctx = new WorkflowContext();
        await step.ExecuteAsync(ctx);
        await step.ExecuteAsync(ctx);
        await step.ExecuteAsync(ctx);

        await Given("ExecutionCount after 3 invocations", () => step.ExecutionCount)
            .Then("count is 3", count =>
            {
                count.Should().Be(3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("FakeStep stores each execution context in ExecutionContexts"), Fact]
    public async Task StoresExecutionContexts()
    {
        var step = new FakeStep("ctx-step");
        var ctx1 = new WorkflowContext();
        var ctx2 = new WorkflowContext();
        await step.ExecuteAsync(ctx1);
        await step.ExecuteAsync(ctx2);

        await Given("ExecutionContexts list after two invocations", () => step.ExecutionContexts)
            .Then("both contexts are stored in order", ctxs =>
            {
                ctxs.Should().HaveCount(2);
                ctxs[0].Should().BeSameAs(ctx1);
                ctxs[1].Should().BeSameAs(ctx2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("FakeStep with a throwing action propagates the exception"), Fact]
    public async Task FakeStepWithThrowingActionPropagates()
    {
        var step = new FakeStep("throwing-step", _ => throw new InvalidOperationException("boom"));
        Exception? thrown = null;
        try { await step.ExecuteAsync(new WorkflowContext()); }
        catch (Exception ex) { thrown = ex; }
        var count = step.ExecutionCount;

        await Given("the thrown exception and execution count", () => (thrown, count))
            .Then("the exception is InvalidOperationException and count is still incremented", t =>
            {
                t.thrown.Should().BeOfType<InvalidOperationException>();
                t.count.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("FakeStep<TData> can mutate the typed payload"), Fact]
    public async Task TypedFakeStepMutatesPayload()
    {
        var step = new FakeStep<FakeStepMutableData>("mutate-step", ctx =>
        {
            ctx.Data.Label = "mutated";
            return Task.CompletedTask;
        });
        var data = new FakeStepMutableData { Label = "original" };
        var ctx = new WorkflowContext<FakeStepMutableData>(data);
        await step.ExecuteAsync(ctx);

        await Given("data after typed FakeStep execution", () => ctx.Data)
            .Then("the data label is 'mutated'", d =>
            {
                d.Label.Should().Be("mutated");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("FakeStep<TData> ExecutionCount increments for typed steps"), Fact]
    public async Task TypedFakeStepCountsInvocations()
    {
        var step = new FakeStep<FakeStepMutableData>("typed-counter");
        var ctx = new WorkflowContext<FakeStepMutableData>(new FakeStepMutableData());
        await step.ExecuteAsync(ctx);
        await step.ExecuteAsync(ctx);

        await Given("ExecutionCount after 2 typed invocations", () => step.ExecutionCount)
            .Then("count is 2", count =>
            {
                count.Should().Be(2);
                return true;
            })
            .AssertPassed();
    }
}

file sealed class FakeStepMutableData { public string Label { get; set; } = ""; }
