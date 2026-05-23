using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Testing;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Testing;

[Feature("WorkflowTestHarness execution and step overrides")]
public class WorkflowTestHarnessTests : TinyBddTestBase
{
    public WorkflowTestHarnessTests(ITestOutputHelper output) : base(output) { }

    [Scenario("ExecuteAsync with no overrides runs all original steps"), Fact]
    public async Task ExecuteWithNoOverridesRunsAllSteps()
    {
        var executed = new List<string>();
        var workflow = Workflow.Create("all-steps")
            .Step(new LambdaStep("s1", _ => { executed.Add("s1"); return Task.CompletedTask; }))
            .Step(new LambdaStep("s2", _ => { executed.Add("s2"); return Task.CompletedTask; }))
            .Build();

        var harness = new WorkflowTestHarness();
        var ctx = new WorkflowContext();
        var result = await harness.ExecuteAsync(workflow, ctx);

        await Given("result and step execution list", () => (result, executed))
            .Then("both steps ran and result is successful", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.executed.Should().Equal("s1", "s2");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("OverrideStep swaps in a FakeStep by name"), Fact]
    public async Task OverrideStepSwapsInFake()
    {
        var originalRan = false;
        var workflow = Workflow.Create("override-test")
            .Step(new LambdaStep("expensive", _ => { originalRan = true; return Task.CompletedTask; }))
            .Build();

        var fake = new FakeStep("expensive");
        var harness = new WorkflowTestHarness();
        harness.OverrideStep("expensive", fake);
        var result = await harness.ExecuteAsync(workflow, new WorkflowContext());

        await Given("result and override state", () => (result, fake, originalRan))
            .Then("the fake ran instead of the original", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.fake.ExecutionCount.Should().Be(1);
                t.originalRan.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple overrides each apply to their respective step"), Fact]
    public async Task MultipleOverridesApplyIndependently()
    {
        var workflow = Workflow.Create("multi-override")
            .Step(new LambdaStep("a", _ => Task.CompletedTask))
            .Step(new LambdaStep("b", _ => Task.CompletedTask))
            .Step(new LambdaStep("c", _ => Task.CompletedTask))
            .Build();

        var fakeA = new FakeStep("a");
        var fakeC = new FakeStep("c");
        var harness = new WorkflowTestHarness();
        harness.OverrideStep("a", fakeA);
        harness.OverrideStep("c", fakeC);
        await harness.ExecuteAsync(workflow, new WorkflowContext());

        await Given("fakes after multi-override execution", () => (fakeA, fakeC))
            .Then("both fakes were invoked exactly once", t =>
            {
                t.fakeA.ExecutionCount.Should().Be(1);
                t.fakeC.ExecutionCount.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync<TData> returns a typed result with mutated data"), Fact]
    public async Task TypedExecuteReturnsTypedResult()
    {
        var workflow = Workflow.Create<HarnessNumberData>("typed-wf")
            .Step("double", ctx => { ctx.Data.Value *= 2; return Task.CompletedTask; })
            .Build();

        var harness = new WorkflowTestHarness();
        var result = await harness.ExecuteAsync(workflow, new HarnessNumberData { Value = 7 });

        await Given("result from typed workflow execution", () => result)
            .Then("result is successful and data is doubled", r =>
            {
                r.IsSuccess.Should().BeTrue();
                r.Data.Value.Should().Be(14);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("OverrideStep via lambda swaps in an inline action"), Fact]
    public async Task OverrideStepViaLambdaWorks()
    {
        var sideEffect = new List<string>();
        var workflow = Workflow.Create("lambda-override")
            .Step(new LambdaStep("original", _ => Task.CompletedTask))
            .Build();

        var harness = new WorkflowTestHarness();
        harness.OverrideStep("original", (IWorkflowContext ctx) =>
        {
            sideEffect.Add("lambda-ran");
            return Task.CompletedTask;
        });
        await harness.ExecuteAsync(workflow, new WorkflowContext());

        await Given("side effects after lambda override", () => sideEffect)
            .Then("the lambda was invoked once", effects =>
            {
                effects.Should().ContainSingle("lambda-ran");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync<TData> without overrides runs the typed workflow directly"), Fact]
    public async Task TypedExecuteWithoutOverridesRunsTypedWorkflow()
    {
        var ran = false;
        var workflow = Workflow.Create<HarnessNumberData>("typed-no-override")
            .Step("compute", ctx => { ran = true; ctx.Data.Value = 42; return Task.CompletedTask; })
            .Build();

        var harness = new WorkflowTestHarness(); // no overrides configured
        var result = await harness.ExecuteAsync(workflow, new HarnessNumberData { Value = 0 });

        await Given("typed result with no harness overrides", () => (result, ran))
            .Then("original step ran and data is mutated", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.result.Data.Value.Should().Be(42);
                t.ran.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync chains OverrideStep calls via fluent builder"), Fact]
    public async Task OverrideStepIsChainable()
    {
        var workflow = Workflow.Create("fluent")
            .Step(new LambdaStep("a", _ => Task.CompletedTask))
            .Step(new LambdaStep("b", _ => Task.CompletedTask))
            .Build();

        var fakeA = new FakeStep("a");
        var fakeB = new FakeStep("b");
        var harness = new WorkflowTestHarness()
            .OverrideStep("a", fakeA)
            .OverrideStep("b", fakeB);

        await harness.ExecuteAsync(workflow, new WorkflowContext());

        await Given("harness configured with two chained overrides", () => (fakeA, fakeB))
            .Then("both fakes executed once", t =>
            {
                t.fakeA.ExecutionCount.Should().Be(1);
                t.fakeB.ExecutionCount.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync<TData> with overrides and dual-interface workflow applies overrides"), Fact]
    public async Task TypedExecuteWithOverridesAppliesWhenWorkflowImplementsIWorkflow()
    {
        var originalRan = false;
        var dualWorkflow = new DualInterfaceWorkflow<HarnessNumberData>(
            "dual-override",
            new LambdaStep("expensive", _ => { originalRan = true; return Task.CompletedTask; }));

        var fake = new FakeStep("expensive");
        var harness = new WorkflowTestHarness();
        harness.OverrideStep("expensive", fake);
        var result = await harness.ExecuteAsync<HarnessNumberData>(dualWorkflow, new HarnessNumberData { Value = 5 });

        await Given("typed result after override applied via dual-interface workflow", () => (result, fake, originalRan))
            .Then("fake ran instead of original and result is successful", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.fake.ExecutionCount.Should().Be(1);
                t.originalRan.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }
}

file sealed class HarnessNumberData { public int Value { get; set; } }

/// <summary>
/// A test-only workflow that implements both IWorkflow and IWorkflow&lt;TData&gt;
/// so that WorkflowTestHarness can apply typed overrides via the IWorkflow path.
/// </summary>
file sealed class DualInterfaceWorkflow<TData>(string name, params IStep[] steps)
    : IWorkflow, IWorkflow<TData>
    where TData : class
{
    public string Name => name;
    public IReadOnlyList<IStep> Steps => steps;

    public Task<WorkflowResult> ExecuteAsync(IWorkflowContext context) =>
        Workflow.Create(name)
            .Step(steps[0])
            .Build()
            .ExecuteAsync(context);

    public async Task<WorkflowResult<TData>> ExecuteAsync(IWorkflowContext<TData> context)
    {
        var result = await ExecuteAsync((IWorkflowContext)context).ConfigureAwait(false);
        return new WorkflowResult<TData>(result.Status, context);
    }
}
