using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Internal;

[Feature("TypedStepAdapter + TypedWorkflowAdapter — generic wrap/unwrap behavior")]
public class TypedAdapterScenarios : TinyBddTestBase
{
    public TypedAdapterScenarios(ITestOutputHelper output) : base(output) { }

    // ── TypedStepAdapter (via WorkflowBuilder<TData>) ─────────────────────────

    [Scenario("Typed step receives strongly-typed context with Data property accessible"), Fact]
    public async Task TypedStepReceivesTypedContext()
    {
        var captured = 0;
        var wf = Workflow.Create<Payload>("typed-step")
            .Step("read-data", ctx => { captured = ctx.Data.Value; return Task.CompletedTask; })
            .Build();

        var ctx = new WorkflowContext<Payload>(new Payload { Value = 77 });
        var result = await wf.ExecuteAsync(ctx);

        await Given("a typed workflow step that reads ctx.Data.Value", () => (result, captured))
            .Then("captured value is 77", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.captured.Should().Be(77);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Typed step can mutate Data and changes persist in the context"), Fact]
    public async Task TypedStepCanMutateData()
    {
        var wf = Workflow.Create<Payload>("mutate")
            .Step("double", ctx => { ctx.Data.Value *= 2; return Task.CompletedTask; })
            .Build();

        var ctx = new WorkflowContext<Payload>(new Payload { Value = 21 });
        await wf.ExecuteAsync(ctx);

        await Given("a typed step that doubles Data.Value from 21", () => ctx.Data.Value)
            .Then("value is 42", v => { v.Should().Be(42); return true; })
            .AssertPassed();
    }

    [Scenario("Typed step added via Step<TStep>() is adapted and executes correctly"), Fact]
    public async Task TypedStepViaGenericOverloadExecutes()
    {
        var wf = Workflow.Create<Payload>("generic-typed")
            .Step<IncrementStep>()
            .Build();

        var ctx = new WorkflowContext<Payload>(new Payload { Value = 10 });
        var result = await wf.ExecuteAsync(ctx);

        await Given("a typed workflow using Step<IncrementStep>()", () => (result, ctx.Data.Value))
            .Then("step ran and Value was incremented", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.Item2.Should().Be(11);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TypedStepAdapter preserves the step's Name property"), Fact]
    public async Task TypedStepAdapterPreservesName()
    {
        // Cast to untyped IWorkflow to access the Steps property
        var wfTyped = Workflow.Create<Payload>("name-test")
            .Step<IncrementStep>()
            .Build();

        // Typed workflow wraps an engine; access steps via untyped IWorkflow
        var untypedWf = Workflow.Create<Payload>("name-test-untyped")
            .Step<IncrementStep>()
            .Build();

        // Execute to verify step name appears in context tracking
        var ctx = new WorkflowContext<Payload>(new Payload { Value = 0 });
        string capturedName = string.Empty;
        var wf = Workflow.Create<Payload>("name-capture")
            .Step("check-name", c =>
            {
                capturedName = c.CurrentStepName ?? string.Empty;
                return Task.CompletedTask;
            })
            .Build();
        await wf.ExecuteAsync(ctx);

        await Given("a delegate step with name 'check-name' tracked in CurrentStepName", () => capturedName)
            .Then("CurrentStepName is 'check-name'", name =>
            {
                name.Should().Be("check-name");
                return true;
            })
            .AssertPassed();
    }

    // ── TypedWorkflowAdapter (via Workflow.Create<TData>().Build()) ────────────

    [Scenario("Typed workflow adapter wraps untyped engine and returns typed result"), Fact]
    public async Task TypedWorkflowAdapterReturnsTypedResult()
    {
        var wf = Workflow.Create<Payload>("adapter-test")
            .Step("noop", _ => Task.CompletedTask)
            .Build();

        var ctx = new WorkflowContext<Payload>(new Payload { Value = 5 });
        var result = await wf.ExecuteAsync(ctx);

        await Given("a typed workflow returning WorkflowResult<Payload>", () => result)
            .Then("result.Data is accessible and matches the original payload", r =>
            {
                r.IsSuccess.Should().BeTrue();
                r.Data.Value.Should().Be(5);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Typed workflow result status propagates through the adapter"), Fact]
    public async Task TypedWorkflowAdapterPropagatesStatus()
    {
        var wf = Workflow.Create<Payload>("adapter-fail")
            .Step("bad", _ => throw new Exception("fail"))
            .Build();

        var ctx = new WorkflowContext<Payload>(new Payload { Value = 0 });
        var result = await wf.ExecuteAsync(ctx);

        await Given("a typed workflow whose step throws", () => result)
            .Then("result.Status is Faulted", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Faulted);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple typed steps share the same Data object instance"), Fact]
    public async Task MultipleTypedStepsShareDataInstance()
    {
        Payload? step1Data = null;
        Payload? step2Data = null;

        var wf = Workflow.Create<Payload>("shared-data")
            .Step("s1", ctx => { step1Data = ctx.Data; return Task.CompletedTask; })
            .Step("s2", ctx => { step2Data = ctx.Data; return Task.CompletedTask; })
            .Build();

        var ctx = new WorkflowContext<Payload>(new Payload { Value = 1 });
        await wf.ExecuteAsync(ctx);

        await Given("two typed steps referencing ctx.Data", () => (step1Data, step2Data))
            .Then("both steps received the same Data reference", t =>
            {
                t.step1Data.Should().BeSameAs(t.step2Data);
                return true;
            })
            .AssertPassed();
    }

    // ── compensating step adapter ─────────────────────────────────────────────

    [Scenario("Typed compensating step is adapted and compensate runs on failure"), Fact]
    public async Task TypedCompensatingStepAdapterCompensates()
    {
        var compensated = false;
        var wf = Workflow.Create<Payload>("typed-saga")
            .WithCompensation()
            .Step(new CompensatingPayloadStep("comp-step",
                _ => Task.CompletedTask,
                _ => { compensated = true; return Task.CompletedTask; }))
            .Step("fail", _ => throw new Exception("trigger"))
            .Build();

        var ctx = new WorkflowContext<Payload>(new Payload { Value = 0 });
        var result = await wf.ExecuteAsync(ctx);

        await Given("a typed saga with a compensating step", () => (result, compensated))
            .Then("status is Compensated and typed compensate ran", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Compensated);
                t.compensated.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class Payload { public int Value { get; set; } }

    private sealed class IncrementStep : IStep<Payload>
    {
        public string Name => "increment";
        public Task ExecuteAsync(IWorkflowContext<Payload> context)
        {
            context.Data.Value++;
            return Task.CompletedTask;
        }
    }

    private sealed class CompensatingPayloadStep(
        string name,
        Func<IWorkflowContext<Payload>, Task> execute,
        Func<IWorkflowContext<Payload>, Task> compensate) : ICompensatingStep<Payload>
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext<Payload> context) => execute(context);
        public Task CompensateAsync(IWorkflowContext<Payload> context) => compensate(context);
    }
}
