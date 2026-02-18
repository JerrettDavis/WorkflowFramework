using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowBuilderCoreTests
{
    [Fact]
    public void Step_NullStep_ThrowsArgumentNullException()
    {
        var builder = new WorkflowBuilder();
        var act = () => builder.Step((IStep)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("step");
    }

    [Fact]
    public void Step_GenericType_AddsStep()
    {
        var wf = new WorkflowBuilder().Step<ParameterlessStep>().Build();
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Step_DelegateOverload_AddsStep()
    {
        var wf = new WorkflowBuilder()
            .Step("inline", ctx => Task.CompletedTask)
            .Build();
        wf.Steps.Should().HaveCount(1);
        wf.Steps[0].Name.Should().Be("inline");
    }

    [Fact]
    public void Use_NullMiddleware_ThrowsArgumentNullException()
    {
        var builder = new WorkflowBuilder();
        var act = () => builder.Use((IWorkflowMiddleware)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("middleware");
    }

    [Fact]
    public void WithEvents_NullEvents_ThrowsArgumentNullException()
    {
        var builder = new WorkflowBuilder();
        var act = () => builder.WithEvents(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("events");
    }

    [Fact]
    public void WithServiceProvider_NullProvider_ThrowsArgumentNullException()
    {
        var builder = new WorkflowBuilder();
        var act = () => builder.WithServiceProvider(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("serviceProvider");
    }

    [Fact]
    public void WithName_NullName_ThrowsArgumentNullException()
    {
        var builder = new WorkflowBuilder();
        var act = () => builder.WithName(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void WithName_SetsWorkflowName()
    {
        var wf = new WorkflowBuilder().WithName("MyWf").Build();
        wf.Name.Should().Be("MyWf");
    }

    [Fact]
    public void Build_DefaultName_IsWorkflow()
    {
        var wf = new WorkflowBuilder().Build();
        wf.Name.Should().Be("Workflow");
    }

    [Fact]
    public async Task If_ThenElse_ConditionTrue_ExecutesThenBranch()
    {
        var wf = Workflow.Create("test")
            .If(ctx => true)
            .Then(new TrackingStep("Then"))
            .Else(new TrackingStep("Else"))
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().Contain("Then").And.NotContain("Else");
    }

    [Fact]
    public async Task If_ThenElse_ConditionFalse_ExecutesElseBranch()
    {
        var wf = Workflow.Create("test")
            .If(ctx => false)
            .Then(new TrackingStep("Then"))
            .Else(new TrackingStep("Else"))
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().Contain("Else").And.NotContain("Then");
    }

    [Fact]
    public async Task If_ThenEndIf_ConditionFalse_NothingExecutes()
    {
        var wf = Workflow.Create("test")
            .If(ctx => false)
            .Then(new TrackingStep("Then"))
            .EndIf()
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().BeEmpty();
    }

    [Fact]
    public async Task If_ThenGeneric_EndIf()
    {
        var wf = Workflow.Create("test")
            .If(ctx => true)
            .Then<ParameterlessStep>()
            .EndIf()
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().Contain("ParameterlessStep");
    }

    [Fact]
    public async Task If_ElseGeneric()
    {
        var wf = Workflow.Create("test")
            .If(ctx => false)
            .Then(new TrackingStep("Then"))
            .Else<ParameterlessStep>()
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().Contain("ParameterlessStep");
    }

    [Fact]
    public async Task Parallel_ExecutesAllBranches()
    {
        var wf = Workflow.Create("test")
            .Parallel(p => p.Step(new TrackingStep("P1")).Step(new TrackingStep("P2")))
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().Contain("P1").And.Contain("P2");
    }

    [Fact]
    public async Task Parallel_GenericStep()
    {
        var wf = Workflow.Create("test")
            .Parallel(p => p.Step<ParameterlessStep>())
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().HaveCount(1);
    }

    [Fact]
    public void Parallel_NullStep_ThrowsArgumentNullException()
    {
        var builder = Workflow.Create("test");
        var act = () => builder.Parallel(p => p.Step((IStep)null!));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithCompensation_EnablesCompensation()
    {
        // Just verify it doesn't throw and builds
        var wf = new WorkflowBuilder().WithCompensation().Build();
        wf.Should().NotBeNull();
    }

    [Fact]
    public async Task UseGenericMiddleware_Works()
    {
        var wf = new WorkflowBuilder()
            .Use<NoOpMiddleware>()
            .Step(new TrackingStep("A"))
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().Contain("A");
    }

    private class NoOpMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => next(context);
    }

    private class ParameterlessStep : IStep
    {
        public string Name => "ParameterlessStep";
        public Task ExecuteAsync(IWorkflowContext context)
        {
            TrackingStep.GetLog(context).Add(Name);
            return Task.CompletedTask;
        }
    }
}
