using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Registry;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Registry;

[Feature("WorkflowRegistry — register, lookup, typed registration")]
public class WorkflowRegistryScenarios : TinyBddTestBase
{
    public WorkflowRegistryScenarios(ITestOutputHelper output) : base(output) { }

    // ── untyped registration & lookup ─────────────────────────────────────────

    [Scenario("Register and Resolve by name returns a fresh workflow instance"), Fact]
    public async Task RegisterAndResolveReturnsWorkflow()
    {
        var registry = new WorkflowRegistry();
        registry.Register("my-workflow", () => Workflow.Create("my-workflow").Build());

        var wf = registry.Resolve("my-workflow");

        await Given("a registry with 'my-workflow' registered", () => wf)
            .Then("resolved workflow is not null and has the correct name", w =>
            {
                w.Should().NotBeNull();
                w.Name.Should().Be("my-workflow");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Resolve calls the factory each time — fresh instances are returned"), Fact]
    public async Task ResolveCallsFactoryEachTime()
    {
        var registry = new WorkflowRegistry();
        registry.Register("fresh", () => Workflow.Create("fresh").Build());

        var wf1 = registry.Resolve("fresh");
        var wf2 = registry.Resolve("fresh");

        await Given("two Resolve calls for the same name", () => (wf1, wf2))
            .Then("different instances are returned", t =>
            {
                t.wf1.Should().NotBeSameAs(t.wf2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Resolve for an unknown name throws KeyNotFoundException"), Fact]
    public async Task ResolveUnknownNameThrows()
    {
        var registry = new WorkflowRegistry();
        Exception? caught = null;
        try { registry.Resolve("does-not-exist"); }
        catch (KeyNotFoundException ex) { caught = ex; }

        await Given("Resolve called for an unregistered name", () => caught)
            .Then("KeyNotFoundException is thrown", ex =>
            {
                ex.Should().BeOfType<KeyNotFoundException>();
                ex!.Message.Should().Contain("does-not-exist");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Registration is case-insensitive — resolve works regardless of casing"), Fact]
    public async Task RegistrationIsCaseInsensitive()
    {
        var registry = new WorkflowRegistry();
        registry.Register("MyWorkflow", () => Workflow.Create("MyWorkflow").Build());

        var wf1 = registry.Resolve("myworkflow");
        var wf2 = registry.Resolve("MYWORKFLOW");

        await Given("workflow registered as 'MyWorkflow', resolved with different casings", () => (wf1, wf2))
            .Then("both resolutions succeed", t =>
            {
                t.wf1.Should().NotBeNull();
                t.wf2.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Re-registering the same name overwrites the previous factory"), Fact]
    public async Task ReRegistrationOverwritesPreviousFactory()
    {
        var registry = new WorkflowRegistry();
        registry.Register("overwrite", () => Workflow.Create("v1").Build());
        registry.Register("overwrite", () => Workflow.Create("v2").Build());

        var wf = registry.Resolve("overwrite");

        await Given("same name registered twice with different factories", () => wf.Name)
            .Then("the second registration wins", n => { n.Should().Be("v2"); return true; })
            .AssertPassed();
    }

    [Scenario("Names property returns all registered workflow names"), Fact]
    public async Task NamesReturnsAllRegisteredNames()
    {
        var registry = new WorkflowRegistry();
        registry.Register("wf-a", () => Workflow.Create("a").Build());
        registry.Register("wf-b", () => Workflow.Create("b").Build());

        var names = registry.Names;

        await Given("two workflows registered", () => names)
            .Then("Names contains both registered names", n =>
            {
                n.Should().Contain("wf-a");
                n.Should().Contain("wf-b");
                n.Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register with null name throws ArgumentNullException"), Fact]
    public async Task RegisterNullNameThrows()
    {
        var registry = new WorkflowRegistry();
        Exception? caught = null;
        try { registry.Register(null!, () => Workflow.Create("x").Build()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null name passed to Register()", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register with null factory throws ArgumentNullException"), Fact]
    public async Task RegisterNullFactoryThrows()
    {
        var registry = new WorkflowRegistry();
        Exception? caught = null;
        try { registry.Register("name", (Func<IWorkflow>)null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null factory passed to Register()", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ── typed registration ────────────────────────────────────────────────────

    [Scenario("Typed workflow can be registered and resolved by name+type"), Fact]
    public async Task TypedWorkflowRegisteredAndResolved()
    {
        var registry = new WorkflowRegistry();
        registry.Register<OrderData>("order-wf", () => Workflow.Create<OrderData>("order-wf").Build());

        var wf = registry.Resolve<OrderData>("order-wf");

        await Given("a typed workflow registered for OrderData", () => wf)
            .Then("resolved workflow is not null", w =>
            {
                w.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Typed Resolve for unknown name throws KeyNotFoundException"), Fact]
    public async Task TypedResolveUnknownNameThrows()
    {
        var registry = new WorkflowRegistry();
        Exception? caught = null;
        try { registry.Resolve<OrderData>("nope"); }
        catch (KeyNotFoundException ex) { caught = ex; }

        await Given("typed Resolve for unregistered name", () => caught)
            .Then("KeyNotFoundException is thrown", ex =>
            {
                ex.Should().BeOfType<KeyNotFoundException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Untyped and typed registrations with same name coexist independently"), Fact]
    public async Task UntypedAndTypedRegistrationsCoexist()
    {
        var registry = new WorkflowRegistry();
        registry.Register("wf", () => Workflow.Create("untyped").Build());
        registry.Register<OrderData>("wf", () => Workflow.Create<OrderData>("typed").Build());

        var untyped = registry.Resolve("wf");
        var typed = registry.Resolve<OrderData>("wf");

        await Given("same name registered both untyped and typed", () => (untyped.Name, typed))
            .Then("both resolve independently", t =>
            {
                t.Item1.Should().Be("untyped");
                t.typed.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    // ── WorkflowRunner ────────────────────────────────────────────────────────

    [Scenario("WorkflowRunner.RunAsync resolves and executes workflow by name"), Fact]
    public async Task WorkflowRunnerRunsWorkflowByName()
    {
        var ran = false;
        var registry = new WorkflowRegistry();
        registry.Register("runner-wf", () => Workflow.Create("runner-wf")
            .Step("s", _ => { ran = true; return Task.CompletedTask; })
            .Build());

        var runner = new WorkflowRunner(registry);
        // Use explicit IWorkflowContext overload to avoid overload resolution picking RunAsync<TData>
        IWorkflowContext ctx = new WorkflowContext();
        var result = await runner.RunAsync("runner-wf", ctx);

        await Given("WorkflowRunner.RunAsync called with a registered workflow name", () => (result, ran))
            .Then("workflow ran and result is successful", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.ran.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WorkflowRunner.RunAsync<TData> creates typed context and executes"), Fact]
    public async Task WorkflowRunnerRunsTypedWorkflow()
    {
        var registry = new WorkflowRegistry();
        registry.Register<OrderData>("typed-runner", () => Workflow.Create<OrderData>("typed-runner")
            .Step("increment", ctx => { ctx.Data.Amount++; return Task.CompletedTask; })
            .Build());

        var runner = new WorkflowRunner(registry);
        var result = await runner.RunAsync("typed-runner", new OrderData { Amount = 5 });

        await Given("WorkflowRunner.RunAsync<OrderData> with Amount=5", () => result)
            .Then("result.Data.Amount is 6", r =>
            {
                r.IsSuccess.Should().BeTrue();
                r.Data.Amount.Should().Be(6);
                return true;
            })
            .AssertPassed();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class OrderData { public int Amount { get; set; } }
}
