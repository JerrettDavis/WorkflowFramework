using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.DependencyInjection;
using Xunit;

namespace WorkflowFramework.Tests.DI;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWorkflowFramework_RegistersBuilder()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        var sp = services.BuildServiceProvider();
        sp.GetService<IWorkflowBuilder>().Should().NotBeNull();
    }

    [Fact]
    public void AddStep_RegistersStep()
    {
        var services = new ServiceCollection();
        services.AddStep<TestStep>();
        var sp = services.BuildServiceProvider();
        sp.GetService<TestStep>().Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflowMiddleware_RegistersMiddleware()
    {
        var services = new ServiceCollection();
        services.AddWorkflowMiddleware<TestMiddleware>();
        var sp = services.BuildServiceProvider();
        sp.GetService<IWorkflowMiddleware>().Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflowEvents_RegistersEvents()
    {
        var services = new ServiceCollection();
        services.AddWorkflowEvents<TestEvents>();
        var sp = services.BuildServiceProvider();
        sp.GetService<IWorkflowEvents>().Should().NotBeNull();
    }

    [Fact]
    public void StepFromServices_ResolvesFromDI()
    {
        var services = new ServiceCollection();
        services.AddTransient<TestStep>();
        var sp = services.BuildServiceProvider();
        var builder = new WorkflowBuilder().WithName("Test");
        builder.StepFromServices<TestStep>(sp);
        var workflow = builder.Build();
        workflow.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void UseFromServices_ResolvesMiddlewareFromDI()
    {
        var services = new ServiceCollection();
        services.AddTransient<TestMiddleware>();
        var sp = services.BuildServiceProvider();
        var builder = new WorkflowBuilder().WithName("Test");
        builder.UseFromServices<TestMiddleware>(sp);
        // Just verifying it doesn't throw - middleware is registered
    }

    [Fact]
    public void AddWorkflowFramework_Returns_ServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddWorkflowFramework();
        result.Should().BeSameAs(services);
    }

    private sealed class TestStep : IStep
    {
        public string Name => "Test";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private sealed class TestMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => next(context);
    }

    private sealed class TestEvents : WorkflowEventsBase { }
}
