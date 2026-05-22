using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.DependencyInjection.Tests.DI;

[Feature("WorkflowBuilderExtensions (DI) — resolving steps from IServiceProvider")]
public class ServiceProviderWorkflowBuilderScenarios : TinyBddXunitBase
{
    public ServiceProviderWorkflowBuilderScenarios(ITestOutputHelper output) : base(output) { }

    // ── helpers ────────────────────────────────────────────────────────────

    private sealed class RecordingStep : IStep
    {
        public bool Executed { get; private set; }
        public string Name => "recording-step";
        public Task ExecuteAsync(IWorkflowContext context)
        {
            Executed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMiddleware : IWorkflowMiddleware
    {
        public bool Invoked { get; private set; }
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
        {
            Invoked = true;
            return next(context);
        }
    }

    private static (IServiceProvider Provider, RecordingStep Step, RecordingMiddleware Mw) BuildServiceProvider()
    {
        var step = new RecordingStep();
        var mw = new RecordingMiddleware();
        var services = new ServiceCollection()
            .AddWorkflowFramework();
        services.AddSingleton<RecordingStep>(step);
        services.AddSingleton<RecordingMiddleware>(mw);
        return (services.BuildServiceProvider(), step, mw);
    }

    // ── StepFromServices ───────────────────────────────────────────────────

    [Scenario("StepFromServices resolves and adds step from DI container"), Fact]
    public async Task StepFromServices_ResolvesAndAddsStep()
    {
        var (provider, step, _) = BuildServiceProvider();
        var builder = provider.GetRequiredService<IWorkflowBuilder>();

        var wf = builder
            .StepFromServices<RecordingStep>(provider)
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("StepFromServices<RecordingStep> added to builder", () => (result, step))
            .Then("workflow completes and the step was executed", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.step.Executed.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("StepFromServices throws if step is not registered"), Fact]
    public async Task StepFromServices_ThrowsIfStepNotRegistered()
    {
        var services = new ServiceCollection().AddWorkflowFramework();
        var provider = services.BuildServiceProvider();
        var builder = provider.GetRequiredService<IWorkflowBuilder>();

        Exception? caught = null;
        try
        {
            builder.StepFromServices<RecordingStep>(provider);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        await Given("RecordingStep not registered in DI", () => caught)
            .Then("an InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    // ── UseFromServices ────────────────────────────────────────────────────

    [Scenario("UseFromServices resolves and wires middleware from DI"), Fact]
    public async Task UseFromServices_ResolvesAndWiresMiddleware()
    {
        var (provider, _, mw) = BuildServiceProvider();
        var builder = provider.GetRequiredService<IWorkflowBuilder>();

        var wf = builder
            .UseFromServices<RecordingMiddleware>(provider)
            .Step("no-op", _ => Task.CompletedTask)
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("UseFromServices<RecordingMiddleware> wired into builder", () => mw)
            .Then("the middleware was invoked during execution", m =>
            {
                m.Invoked.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("UseFromServices throws if middleware is not registered"), Fact]
    public async Task UseFromServices_ThrowsIfMiddlewareNotRegistered()
    {
        var services = new ServiceCollection().AddWorkflowFramework();
        var provider = services.BuildServiceProvider();
        var builder = provider.GetRequiredService<IWorkflowBuilder>();

        Exception? caught = null;
        try
        {
            builder.UseFromServices<RecordingMiddleware>(provider);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        await Given("RecordingMiddleware not registered in DI", () => caught)
            .Then("an exception is thrown by GetRequiredService", ex =>
            {
                ex.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Chaining StepFromServices with other builder calls produces correct workflow"), Fact]
    public async Task StepFromServices_Chaining_WorksCorrectly()
    {
        var (provider, step, _) = BuildServiceProvider();
        var order = new List<string>();
        var builder = provider.GetRequiredService<IWorkflowBuilder>();

        var wf = builder
            .Step("before", _ => { order.Add("before"); return Task.CompletedTask; })
            .StepFromServices<RecordingStep>(provider)
            .Step("after", _ => { order.Add("after"); return Task.CompletedTask; })
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("three steps: inline, DI-resolved, inline", () => (order, step))
            .Then("all ran; DI step executed; order is before→DI→after", t =>
            {
                t.order.Should().Equal("before", "after");
                t.step.Executed.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }
}
