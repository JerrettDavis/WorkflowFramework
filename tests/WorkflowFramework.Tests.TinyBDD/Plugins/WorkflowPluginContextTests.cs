using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Plugins;

namespace WorkflowFramework.Tests.TinyBDD.Plugins;

[Feature("Workflow plugin context")]
public class WorkflowPluginContextTests : TinyBddTestBase
{
    public WorkflowPluginContextTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Services registered via RegisterStep are resolvable from IServiceCollection"), Fact]
    public async Task RegisteredStepIsResolvable()
    {
        var services = new ServiceCollection();
        var ctx = new WorkflowPluginContext(services);
        ctx.RegisterStep<StubStep>();
        var sp = ctx.Services.BuildServiceProvider();

        await Given("a service provider after RegisterStep<StubStep>", () => sp)
            .Then("StubStep can be resolved", provider =>
            {
                provider.GetService<StubStep>().Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RegisterMiddleware adds IWorkflowMiddleware to the service collection"), Fact]
    public async Task RegisteredMiddlewareIsResolvable()
    {
        var ctx = new WorkflowPluginContext(new ServiceCollection());
        ctx.RegisterMiddleware<StubMiddleware>();
        var sp = ctx.Services.BuildServiceProvider();

        await Given("a service provider after RegisterMiddleware<StubMiddleware>", () => sp)
            .Then("IWorkflowMiddleware resolves to StubMiddleware", provider =>
            {
                provider.GetService<IWorkflowMiddleware>().Should().BeOfType<StubMiddleware>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("OnEvent handler is returned by GetEventHooks"), Fact]
    public async Task EventHandlerIsRetrievable()
    {
        var ctx = new WorkflowPluginContext(new ServiceCollection());
        ctx.OnEvent("workflow.start", _ => Task.CompletedTask);
        var hooks = ctx.GetEventHooks("workflow.start");

        await Given("the hooks list for workflow.start after registering one handler", () => hooks)
            .Then("there is exactly one handler", list =>
            {
                list.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple OnEvent handlers for the same event are all returned"), Fact]
    public async Task MultipleHandlersAllReturned()
    {
        var ctx = new WorkflowPluginContext(new ServiceCollection());
        ctx.OnEvent("my.event", _ => Task.CompletedTask);
        ctx.OnEvent("my.event", _ => Task.CompletedTask);
        var hooks = ctx.GetEventHooks("my.event");

        await Given("the hooks list for my.event after registering two handlers", () => hooks)
            .Then("there are two handlers", list =>
            {
                list.Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetEventHooks for unknown event returns empty list"), Fact]
    public async Task UnknownEventReturnsEmpty()
    {
        var ctx = new WorkflowPluginContext(new ServiceCollection());
        var hooks = ctx.GetEventHooks("no.such.event");

        await Given("the hooks list for an unregistered event", () => hooks)
            .Then("the result is empty", list =>
            {
                list.Should().BeEmpty();
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
}
