using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class TryCatchStepCoreTests
{
    [Fact]
    public async Task Try_NoError_ExecutesBody()
    {
        var wf = Workflow.Create("test")
            .Try(body => body.Step(new TrackingStep("TryBody")))
            .EndTry()
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().Contain("TryBody");
    }

    [Fact]
    public async Task Try_Catch_HandlesException()
    {
        var caught = false;
        var wf = Workflow.Create("test")
            .Try(body => body.Step(new FailingStep()))
            .Catch<InvalidOperationException>((ctx, ex) => { caught = true; return Task.CompletedTask; })
            .EndTry()
            .Build();
        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);
        result.IsSuccess.Should().BeTrue();
        caught.Should().BeTrue();
    }

    [Fact]
    public async Task Try_Catch_WrongExceptionType_Propagates()
    {
        var wf = Workflow.Create("test")
            .Try(body => body.Step(new FailingStep())) // throws InvalidOperationException
            .Catch<ArgumentException>((ctx, ex) => Task.CompletedTask)
            .EndTry()
            .Build();
        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);
        result.Status.Should().Be(WorkflowStatus.Faulted);
    }

    [Fact]
    public async Task Try_Catch_BaseExceptionType_Catches()
    {
        var caught = false;
        var wf = Workflow.Create("test")
            .Try(body => body.Step(new FailingStep()))
            .Catch<Exception>((ctx, ex) => { caught = true; return Task.CompletedTask; })
            .EndTry()
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        caught.Should().BeTrue();
    }

    [Fact]
    public async Task Try_Finally_AlwaysRuns()
    {
        var finallyRan = false;
        var wf = Workflow.Create("test")
            .Try(body => body.Step(new TrackingStep("Body")))
            .Finally(fin => fin.Step("finally", ctx => { finallyRan = true; return Task.CompletedTask; }))
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        finallyRan.Should().BeTrue();
    }

    [Fact]
    public async Task Try_CatchFinally_ErrorPath_FinallyStillRuns()
    {
        var finallyRan = false;
        var caught = false;
        var wf = Workflow.Create("test")
            .Try(body => body.Step(new FailingStep()))
            .Catch<InvalidOperationException>((ctx, ex) => { caught = true; return Task.CompletedTask; })
            .Finally(fin => fin.Step("finally", ctx => { finallyRan = true; return Task.CompletedTask; }))
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        caught.Should().BeTrue();
        finallyRan.Should().BeTrue();
    }

    [Fact]
    public async Task Try_MultipleCatchHandlers()
    {
        var which = "";
        var wf = Workflow.Create("test")
            .Try(body => body.Step(new FailingStep()))
            .Catch<ArgumentException>((ctx, ex) => { which = "arg"; return Task.CompletedTask; })
            .Catch<InvalidOperationException>((ctx, ex) => { which = "ioe"; return Task.CompletedTask; })
            .EndTry()
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        which.Should().Be("ioe");
    }
}
