using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.DependencyInjection;
using WorkflowFramework.Builder;

namespace WorkflowFramework.Tests.TinyBDD.DI;

[Feature("ServiceCollection extensions for WorkflowFramework")]
public class ServiceCollectionExtensionsTests : TinyBddTestBase
{
    public ServiceCollectionExtensionsTests(ITestOutputHelper output) : base(output) { }

    [Scenario("AddWorkflowFramework registers IWorkflowBuilder"), Fact]
    public async Task AddWorkflowFrameworkRegistersBuilder()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        var sp = services.BuildServiceProvider();

        await Given("a service provider after AddWorkflowFramework", () => sp)
            .Then("IWorkflowBuilder can be resolved", provider =>
            {
                provider.GetService<IWorkflowBuilder>().Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddStep registers the step as a transient"), Fact]
    public async Task AddStepRegistersTransient()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        services.AddStep<StubStep>();
        var sp = services.BuildServiceProvider();

        await Given("a service provider after AddStep<StubStep>", () => sp)
            .Then("StubStep can be resolved", provider =>
            {
                provider.GetService<StubStep>().Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowMiddleware registers IWorkflowMiddleware"), Fact]
    public async Task AddMiddlewareRegistersInterface()
    {
        var services = new ServiceCollection();
        services.AddWorkflowMiddleware<StubMiddleware>();
        var sp = services.BuildServiceProvider();

        await Given("a service provider after AddWorkflowMiddleware<StubMiddleware>", () => sp)
            .Then("IWorkflowMiddleware resolves to StubMiddleware", provider =>
            {
                provider.GetService<IWorkflowMiddleware>().Should().BeOfType<StubMiddleware>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowEvents registers IWorkflowEvents"), Fact]
    public async Task AddEventsRegistersInterface()
    {
        var services = new ServiceCollection();
        services.AddWorkflowEvents<StubEvents>();
        var sp = services.BuildServiceProvider();

        await Given("a service provider after AddWorkflowEvents<StubEvents>", () => sp)
            .Then("IWorkflowEvents resolves to StubEvents", provider =>
            {
                provider.GetService<IWorkflowEvents>().Should().BeOfType<StubEvents>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Service provider validates scopes without errors after AddWorkflowFramework"), Fact]
    public async Task ServiceProviderValidatesScopes()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        Action build = () => services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await Given("a build action that creates a scope-validated service provider", () => build)
            .Then("no exception is thrown", b =>
            {
                b.Should().NotThrow();
                return true;
            })
            .AssertPassed();
    }

    // -- Stubs --

    private sealed class StubStep : IStep
    {
        public string Name => "stub";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private sealed class StubMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => next(context);
    }

    private sealed class StubEvents : WorkflowEventsBase { }
}
