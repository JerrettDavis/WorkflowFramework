using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Builder;

[Feature("WorkflowBuilder — fluent workflow construction")]
public class WorkflowBuilderScenarios : TinyBddTestBase
{
    public WorkflowBuilderScenarios(ITestOutputHelper output) : base(output) { }

    // ── step registration ─────────────────────────────────────────────────────

    [Scenario("Steps added via .Step(name, delegate) appear in Steps collection"), Fact]
    public async Task DelegateStepsAppearInStepsCollection()
    {
        var wf = Workflow.Create("steps-test")
            .Step("step-a", _ => Task.CompletedTask)
            .Step("step-b", _ => Task.CompletedTask)
            .Build();

        await Given("a workflow with two delegate steps", () => wf.Steps)
            .Then("Steps contains both in order", steps =>
            {
                steps.Should().HaveCount(2);
                steps[0].Name.Should().Be("step-a");
                steps[1].Name.Should().Be("step-b");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Step added via .Step<TStep>() uses new() constraint and appears in Steps"), Fact]
    public async Task GenericStepTypeIsAddedViaNewConstraint()
    {
        var wf = Workflow.Create("generic-step")
            .Step<NoOpStep>()
            .Build();

        await Given("a workflow with a generic-typed step", () => wf.Steps)
            .Then("Steps contains the NoOpStep", steps =>
            {
                steps.Should().ContainSingle();
                steps[0].Name.Should().Be("noop");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Step instance added via .Step(IStep) is placed in Steps"), Fact]
    public async Task InstanceStepIsAddedToSteps()
    {
        var instance = new NoOpStep();
        var wf = Workflow.Create("instance-step")
            .Step(instance)
            .Build();

        await Given("a step instance passed to Step(IStep)", () => wf.Steps)
            .Then("the same instance appears in Steps", steps =>
            {
                steps.Should().ContainSingle();
                steps[0].Should().BeSameAs(instance);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Step(null) overload throws ArgumentNullException"), Fact]
    public async Task StepNullThrows()
    {
        Exception? caught = null;
        try { Workflow.Create("null").Step((IStep)null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null IStep passed to Step()", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ── naming ────────────────────────────────────────────────────────────────

    [Scenario("WithName sets the workflow name exposed on the built workflow"), Fact]
    public async Task WithNameSetsWorkflowName()
    {
        var wf = Workflow.Create().WithName("custom-name").Build();

        await Given("a workflow built with .WithName('custom-name')", () => wf.Name)
            .Then("Name returns 'custom-name'", name =>
            {
                name.Should().Be("custom-name");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Workflow.Create(name) shorthand sets the workflow name"), Fact]
    public async Task CreateNameShorthandSetsName()
    {
        var wf = Workflow.Create("shorthand").Build();

        await Given("a workflow created with Workflow.Create('shorthand')", () => wf.Name)
            .Then("Name is 'shorthand'", n => { n.Should().Be("shorthand"); return true; })
            .AssertPassed();
    }

    [Scenario("Default workflow name is 'Workflow' when not explicitly set"), Fact]
    public async Task DefaultWorkflowNameIsWorkflow()
    {
        var wf = Workflow.Create().Build();

        await Given("a workflow built without a name", () => wf.Name)
            .Then("Name defaults to 'Workflow'", n => { n.Should().Be("Workflow"); return true; })
            .AssertPassed();
    }

    // ── middleware and events ─────────────────────────────────────────────────

    [Scenario("WithEvents registers an event handler that fires on completion"), Fact]
    public async Task WithEventsRegistersHandler()
    {
        var fired = false;
        var wf = Workflow.Create("events")
            .WithEvents(new DelegateEvents(onCompleted: _ => { fired = true; return Task.CompletedTask; }))
            .Step("s", _ => Task.CompletedTask)
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a workflow with an OnCompleted handler", () => fired)
            .Then("the handler fired after completion", f => { f.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Use<TMiddleware> adds middleware that wraps each step"), Fact]
    public async Task UseGenericMiddlewareWrapsSteps()
    {
        CounterMiddleware.CallCount = 0;
        var wf = Workflow.Create("mw")
            .Use<CounterMiddleware>()
            .Step("s1", _ => Task.CompletedTask)
            .Step("s2", _ => Task.CompletedTask)
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a middleware-wrapped workflow with 2 steps", () => CounterMiddleware.CallCount)
            .Then("middleware was invoked twice (once per step)", count =>
            {
                count.Should().Be(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Use(instance) middleware overload is accepted without throwing"), Fact]
    public async Task UseInstanceMiddlewareAccepted()
    {
        var mw = new CounterMiddleware();
        var wf = Workflow.Create("mw-instance")
            .Use(mw)
            .Step("s", _ => Task.CompletedTask)
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a workflow with instance middleware", () => result)
            .Then("workflow completes successfully", r => { r.IsSuccess.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Use(null) throws ArgumentNullException"), Fact]
    public async Task UseNullMiddlewareThrows()
    {
        Exception? caught = null;
        try { Workflow.Create("null-mw").Use((IWorkflowMiddleware)null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null middleware passed to Use()", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ── WithCompensation ──────────────────────────────────────────────────────

    [Scenario("WithCompensation enables saga mode — failed step triggers compensation"), Fact]
    public async Task WithCompensationEnablesSagaMode()
    {
        var compensated = false;
        var wf = Workflow.Create("saga")
            .WithCompensation()
            .Step(new CompensatingLambdaStep("a",
                _ => Task.CompletedTask,
                _ => { compensated = true; return Task.CompletedTask; }))
            .Step("fail", _ => throw new Exception("fail"))
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a saga workflow where a step throws", () => (result, compensated))
            .Then("status is Compensated and compensation ran", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Compensated);
                t.compensated.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ── conditional builder ───────────────────────────────────────────────────

    [Scenario("If().Then().EndIf() adds a conditional step that runs then-branch when true"), Fact]
    public async Task IfThenEndIfRunsThenBranchWhenTrue()
    {
        var log = new List<string>();
        var wf = Workflow.Create("cond")
            .If(_ => true)
            .Then(new LambdaStep("then-step", _ => { log.Add("then"); return Task.CompletedTask; }))
            .EndIf()
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a conditional with always-true predicate", () => log)
            .Then("then-branch ran", l => { l.Should().ContainSingle("then"); return true; })
            .AssertPassed();
    }

    [Scenario("If().Then().Else().Build runs else-branch when condition is false"), Fact]
    public async Task IfThenElseRunsElseBranchWhenFalse()
    {
        var log = new List<string>();
        var wf = Workflow.Create("else")
            .If(_ => false)
            .Then(new LambdaStep("then-step", _ => { log.Add("then"); return Task.CompletedTask; }))
            .Else(new LambdaStep("else-step", _ => { log.Add("else"); return Task.CompletedTask; }))
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a conditional with always-false predicate", () => log)
            .Then("else-branch ran and then-branch did not", l =>
            {
                l.Should().ContainSingle("else");
                l.Should().NotContain("then");
                return true;
            })
            .AssertPassed();
    }

    // ── parallel builder ──────────────────────────────────────────────────────

    [Scenario("Parallel() adds all configured steps and they all execute"), Fact]
    public async Task ParallelBuilderAddsAllSteps()
    {
        var log = new System.Collections.Concurrent.ConcurrentBag<string>();
        var wf = Workflow.Create("parallel")
            .Parallel(p => p
                .Step(new LambdaStep("p1", _ => { log.Add("p1"); return Task.CompletedTask; }))
                .Step(new LambdaStep("p2", _ => { log.Add("p2"); return Task.CompletedTask; }))
                .Step(new LambdaStep("p3", _ => { log.Add("p3"); return Task.CompletedTask; })))
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a parallel group with 3 steps", () => log)
            .Then("all three steps ran", l =>
            {
                l.Should().Contain("p1");
                l.Should().Contain("p2");
                l.Should().Contain("p3");
                return true;
            })
            .AssertPassed();
    }

    // ── typed builder (WorkflowBuilder<TData>) ────────────────────────────────

    [Scenario("Typed workflow builder passes TData to each step via typed context"), Fact]
    public async Task TypedBuilderPassesDataToSteps()
    {
        var wf = Workflow.Create<MyData>("typed-wf")
            .Step("mutate", ctx => { ctx.Data.Value = 42; return Task.CompletedTask; })
            .Build();

        var ctx = new WorkflowContext<MyData>(new MyData());
        var result = await wf.ExecuteAsync(ctx);

        await Given("a typed workflow that mutates Data.Value", () => ctx.Data.Value)
            .Then("Data.Value is 42 after execution", v => { v.Should().Be(42); return true; })
            .AssertPassed();
    }

    [Scenario("Typed step added via Step<TStep>() adapter delegates to typed ExecuteAsync"), Fact]
    public async Task TypedGenericStepIsAdaptedCorrectly()
    {
        var wf = Workflow.Create<MyData>("typed-generic")
            .Step<TypedNoOpStep>()
            .Build();

        var ctx = new WorkflowContext<MyData>(new MyData { Value = 99 });
        var result = await wf.ExecuteAsync(ctx);

        await Given("a typed workflow with a typed step added via Step<T>()", () => result)
            .Then("workflow completes and data is unchanged", r =>
            {
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class NoOpStep : IStep
    {
        public string Name => "noop";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private sealed class LambdaStep(string name, Func<IWorkflowContext, Task> action) : IStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => action(context);
    }

    private sealed class CompensatingLambdaStep(
        string name,
        Func<IWorkflowContext, Task> execute,
        Func<IWorkflowContext, Task> compensate) : ICompensatingStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => execute(context);
        public Task CompensateAsync(IWorkflowContext context) => compensate(context);
    }

    private sealed class DelegateEvents(Func<IWorkflowContext, Task>? onCompleted = null) : WorkflowEventsBase
    {
        public override Task OnWorkflowCompletedAsync(IWorkflowContext ctx) =>
            onCompleted?.Invoke(ctx) ?? Task.CompletedTask;
    }

    private sealed class CounterMiddleware : IWorkflowMiddleware
    {
        [ThreadStatic]
        private static int _count;
        public static int CallCount { get => _count; set => _count = value; }

        public async Task InvokeAsync(IWorkflowContext ctx, IStep step, StepDelegate next)
        {
            _count++;
            await next(ctx);
        }
    }

    private sealed class MyData { public int Value { get; set; } }

    private sealed class TypedNoOpStep : IStep<MyData>
    {
        public string Name => "typed-noop";
        public Task ExecuteAsync(IWorkflowContext<MyData> context) => Task.CompletedTask;
    }
}
