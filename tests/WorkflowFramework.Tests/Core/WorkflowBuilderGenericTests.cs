using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowBuilderGenericTests
{
    private class OrderData
    {
        public string Status { get; set; } = "new";
        public List<string> Log { get; set; } = new();
    }

    private class SetStatusStep : IStep<OrderData>
    {
        public string Name => "SetStatus";
        public Task ExecuteAsync(IWorkflowContext<OrderData> context)
        {
            context.Data.Status = "processed";
            return Task.CompletedTask;
        }
    }

    private class LogStep : IStep<OrderData>
    {
        private readonly string _msg;
        public LogStep(string msg = "LogStep") => _msg = msg;
        public string Name => _msg;
        public Task ExecuteAsync(IWorkflowContext<OrderData> context)
        {
            context.Data.Log.Add(_msg);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Step_GenericType_Works()
    {
        var wf = Workflow.Create<OrderData>("test")
            .Step<SetStatusStep>()
            .Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        var result = await wf.ExecuteAsync(ctx);
        result.Data.Status.Should().Be("processed");
    }

    [Fact]
    public void Step_NullStep_ThrowsArgumentNullException()
    {
        var builder = new WorkflowBuilder<OrderData>();
        var act = () => builder.Step((IStep<OrderData>)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("step");
    }

    [Fact]
    public async Task Step_Instance_Works()
    {
        var wf = Workflow.Create<OrderData>()
            .Step(new LogStep("Hello"))
            .Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Log.Should().Contain("Hello");
    }

    [Fact]
    public async Task Step_DelegateOverload()
    {
        var wf = Workflow.Create<OrderData>()
            .Step("inline", ctx => { ctx.Data.Log.Add("inline"); return Task.CompletedTask; })
            .Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Log.Should().Contain("inline");
    }

    [Fact]
    public async Task If_Then_Else_Typed()
    {
        var wf = Workflow.Create<OrderData>()
            .If(ctx => ctx.Data.Status == "new")
            .Then(new LogStep("WasNew"))
            .Else(new LogStep("WasOther"))
            .Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Log.Should().Contain("WasNew");
    }

    [Fact]
    public async Task If_Then_EndIf_Typed()
    {
        var wf = Workflow.Create<OrderData>()
            .If(ctx => ctx.Data.Status == "nope")
            .Then(new LogStep("Nope"))
            .EndIf()
            .Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Log.Should().BeEmpty();
    }

    [Fact]
    public async Task If_ThenGeneric_ElseGeneric_Typed()
    {
        var wf = Workflow.Create<OrderData>()
            .If(ctx => true)
            .Then<SetStatusStep>()
            .Else<SetStatusStep>()
            .Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Status.Should().Be("processed");
    }

    [Fact]
    public async Task Parallel_Typed()
    {
        var wf = Workflow.Create<OrderData>()
            .Parallel(p => p.Step(new LogStep("P1")).Step(new LogStep("P2")))
            .Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Log.Should().Contain("P1").And.Contain("P2");
    }

    [Fact]
    public async Task Parallel_GenericStep_Typed()
    {
        var wf = Workflow.Create<OrderData>()
            .Parallel(p => p.Step<SetStatusStep>())
            .Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Status.Should().Be("processed");
    }

    [Fact]
    public void Parallel_NullStep_Throws()
    {
        var builder = Workflow.Create<OrderData>();
        var act = () => builder.Parallel(p => p.Step((IStep<OrderData>)null!));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Use_NullMiddleware_Throws()
    {
        var act = () => new WorkflowBuilder<OrderData>().Use((IWorkflowMiddleware)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithEvents_Null_Throws()
    {
        var act = () => new WorkflowBuilder<OrderData>().WithEvents(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithServiceProvider_Null_Throws()
    {
        var act = () => new WorkflowBuilder<OrderData>().WithServiceProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithName_Null_Throws()
    {
        var act = () => new WorkflowBuilder<OrderData>().WithName(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithName_SetsName()
    {
        var wf = Workflow.Create<OrderData>("MyTypedWf").Build();
        wf.Name.Should().Be("MyTypedWf");
    }

    [Fact]
    public void DefaultName_IsWorkflow()
    {
        var wf = Workflow.Create<OrderData>().Build();
        wf.Name.Should().Be("Workflow");
    }

    [Fact]
    public void WithCompensation_DoesNotThrow()
    {
        var wf = new WorkflowBuilder<OrderData>().WithCompensation().Build();
        wf.Should().NotBeNull();
    }

    [Fact]
    public async Task UseGenericMiddleware_Works()
    {
        var wf = new WorkflowBuilder<OrderData>()
            .Use<PassthroughMiddleware>()
            .Step(new LogStep("A"))
            .Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        await wf.ExecuteAsync(ctx);
        ctx.Data.Log.Should().Contain("A");
    }

    [Fact]
    public async Task Build_ReturnsTypedWorkflow()
    {
        var wf = Workflow.Create<OrderData>().Step<SetStatusStep>().Build();
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        var result = await wf.ExecuteAsync(ctx);
        result.TypedContext.Should().NotBeNull();
        result.Data.Status.Should().Be("processed");
        result.IsSuccess.Should().BeTrue();
    }

    private class PassthroughMiddleware : IWorkflowMiddleware
    {
        public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next) => next(context);
    }
}
