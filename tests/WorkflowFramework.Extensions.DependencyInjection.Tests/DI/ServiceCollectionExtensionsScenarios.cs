using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.DependencyInjection.Tests.DI;

[Feature("ServiceCollectionExtensions — WorkflowFramework DI registration")]
public class ServiceCollectionExtensionsScenarios : TinyBddXunitBase
{
    public ServiceCollectionExtensionsScenarios(ITestOutputHelper output) : base(output) { }

    // ── helpers ────────────────────────────────────────────────────────────

    private static IServiceCollection Fresh() => new ServiceCollection();

    private sealed class TestStep : IStep
    {
        public string Name => "test-step";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private sealed class TestTypedStep : IStep<TestData>
    {
        public string Name => "typed-step";
        public Task ExecuteAsync(IWorkflowContext<TestData> context) => Task.CompletedTask;
    }

    private sealed class TestData { }

    private sealed class TestMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => next(context);
    }

    private sealed class TestEvents : WorkflowEventsBase { }

    // ── AddWorkflowFramework ───────────────────────────────────────────────

    [Scenario("AddWorkflowFramework registers IWorkflowBuilder as transient"), Fact]
    public async Task AddWorkflowFramework_RegistersWorkflowBuilder()
    {
        var services = Fresh().AddWorkflowFramework();
        var provider = services.BuildServiceProvider();

        await Given("AddWorkflowFramework called on empty service collection", () => provider)
            .Then("IWorkflowBuilder is resolvable", p =>
            {
                p.GetService<IWorkflowBuilder>().Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowFramework registers generic IWorkflowBuilder<T>"), Fact]
    public async Task AddWorkflowFramework_RegistersGenericBuilder()
    {
        var services = Fresh().AddWorkflowFramework();
        var provider = services.BuildServiceProvider();

        await Given("AddWorkflowFramework registered", () => provider)
            .Then("IWorkflowBuilder<TestData> is resolvable", p =>
            {
                p.GetService<IWorkflowBuilder<TestData>>().Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Each resolve of IWorkflowBuilder returns a new instance (transient)"), Fact]
    public async Task AddWorkflowFramework_BuilderIsTransient()
    {
        var services = Fresh().AddWorkflowFramework();
        var provider = services.BuildServiceProvider();

        await Given("IWorkflowBuilder registered as transient", () => provider)
            .Then("two resolved instances are not the same reference", p =>
            {
                var b1 = p.GetRequiredService<IWorkflowBuilder>();
                var b2 = p.GetRequiredService<IWorkflowBuilder>();
                b1.Should().NotBeSameAs(b2);
                return true;
            })
            .AssertPassed();
    }

    // ── AddStep<TStep> ─────────────────────────────────────────────────────

    [Scenario("AddStep<T> registers step as transient"), Fact]
    public async Task AddStep_RegistersStepAsTransient()
    {
        var services = Fresh().AddStep<TestStep>();
        var provider = services.BuildServiceProvider();

        await Given("AddStep<TestStep> called", () => provider)
            .Then("TestStep is resolvable", p =>
            {
                p.GetService<TestStep>().Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddStep<TStep,TData> registers typed step as transient"), Fact]
    public async Task AddStep_Typed_RegistersStepAsTransient()
    {
        var services = Fresh().AddStep<TestTypedStep, TestData>();
        var provider = services.BuildServiceProvider();

        await Given("AddStep<TestTypedStep, TestData> called", () => provider)
            .Then("TestTypedStep is resolvable", p =>
            {
                p.GetService<TestTypedStep>().Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    // ── AddWorkflowMiddleware ──────────────────────────────────────────────

    [Scenario("AddWorkflowMiddleware registers middleware as IWorkflowMiddleware"), Fact]
    public async Task AddWorkflowMiddleware_RegistersAsInterface()
    {
        var services = Fresh().AddWorkflowMiddleware<TestMiddleware>();
        var provider = services.BuildServiceProvider();

        await Given("AddWorkflowMiddleware<TestMiddleware> called", () => provider)
            .Then("IWorkflowMiddleware is resolvable", p =>
            {
                var mw = p.GetService<IWorkflowMiddleware>();
                mw.Should().NotBeNull().And.BeOfType<TestMiddleware>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple middleware registrations are all returned via GetServices"), Fact]
    public async Task AddWorkflowMiddleware_MultipleRegistrations_AllResolvable()
    {
        var services = Fresh()
            .AddWorkflowMiddleware<TestMiddleware>()
            .AddWorkflowMiddleware<TestMiddleware>();
        var provider = services.BuildServiceProvider();

        await Given("two middleware registrations", () => provider)
            .Then("GetServices<IWorkflowMiddleware> returns two instances", p =>
            {
                var list = p.GetServices<IWorkflowMiddleware>().ToList();
                list.Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    // ── AddWorkflowEvents ─────────────────────────────────────────────────

    [Scenario("AddWorkflowEvents registers event handler as IWorkflowEvents"), Fact]
    public async Task AddWorkflowEvents_RegistersAsInterface()
    {
        var services = Fresh().AddWorkflowEvents<TestEvents>();
        var provider = services.BuildServiceProvider();

        await Given("AddWorkflowEvents<TestEvents> called", () => provider)
            .Then("IWorkflowEvents resolves to TestEvents instance", p =>
            {
                var ev = p.GetService<IWorkflowEvents>();
                ev.Should().NotBeNull().And.BeOfType<TestEvents>();
                return true;
            })
            .AssertPassed();
    }
}
