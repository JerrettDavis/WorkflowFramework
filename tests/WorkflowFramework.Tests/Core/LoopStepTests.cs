using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class LoopStepCoreTests
{
    [Fact]
    public async Task ForEach_IteratesAllItems()
    {
        var wf = Workflow.Create("test")
            .ForEach<string>(
                ctx => new[] { "A", "B", "C" },
                body => body.Step("log", ctx =>
                {
                    TrackingStep.GetLog(ctx).Add((string)ctx.Properties["ForEach.Current"]!);
                    return Task.CompletedTask;
                }))
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().ContainInOrder("A", "B", "C");
    }

    [Fact]
    public async Task ForEach_SetsIndex()
    {
        var indices = new List<int>();
        var wf = Workflow.Create("test")
            .ForEach<string>(
                ctx => new[] { "x", "y" },
                body => body.Step("log", ctx =>
                {
                    indices.Add((int)ctx.Properties["ForEach.Index"]!);
                    return Task.CompletedTask;
                }))
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        indices.Should().ContainInOrder(0, 1);
    }

    [Fact]
    public async Task ForEach_EmptyCollection_NoExecution()
    {
        var executed = false;
        var wf = Workflow.Create("test")
            .ForEach<string>(
                ctx => Array.Empty<string>(),
                body => body.Step("nope", ctx => { executed = true; return Task.CompletedTask; }))
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task ForEach_RespectsAbort()
    {
        var count = 0;
        var wf = Workflow.Create("test")
            .ForEach<int>(
                ctx => new[] { 1, 2, 3 },
                body => body.Step("inc", ctx =>
                {
                    count++;
                    if (count >= 2) ctx.IsAborted = true;
                    return Task.CompletedTask;
                }))
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        count.Should().Be(2);
    }

    [Fact]
    public async Task While_LoopsWhileTrue()
    {
        var wf = Workflow.Create("test")
            .While(ctx =>
            {
                ctx.Properties.TryGetValue("count", out var v);
                return ((int?)v ?? 0) < 3;
            }, body => body.Step("inc", ctx =>
            {
                ctx.Properties.TryGetValue("count", out var v);
                ctx.Properties["count"] = ((int?)v ?? 0) + 1;
                return Task.CompletedTask;
            }))
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        ctx.Properties["count"].Should().Be(3);
    }

    [Fact]
    public async Task While_FalseInitially_NeverExecutes()
    {
        var executed = false;
        var wf = Workflow.Create("test")
            .While(ctx => false, body => body.Step("nope", ctx => { executed = true; return Task.CompletedTask; }))
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task DoWhile_ExecutesAtLeastOnce()
    {
        var count = 0;
        var wf = Workflow.Create("test")
            .DoWhile(body => body.Step("inc", ctx => { count++; return Task.CompletedTask; }), ctx => false)
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        count.Should().Be(1);
    }

    [Fact]
    public async Task DoWhile_LoopsWhileTrue()
    {
        var count = 0;
        var wf = Workflow.Create("test")
            .DoWhile(
                body => body.Step("inc", ctx => { count++; return Task.CompletedTask; }),
                ctx => count < 3)
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Retry_SucceedsOnFirstAttempt()
    {
        var count = 0;
        var wf = Workflow.Create("test")
            .Retry(body => body.Step("work", ctx => { count++; return Task.CompletedTask; }), 3)
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Retry_RetriesOnFailure()
    {
        var attempt = 0;
        var wf = Workflow.Create("test")
            .Retry(body => body.Step("work", ctx =>
            {
                attempt++;
                if (attempt < 3) throw new Exception("fail");
                return Task.CompletedTask;
            }), 3)
            .Build();
        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);
        result.IsSuccess.Should().BeTrue();
        attempt.Should().Be(3);
    }

    [Fact]
    public async Task Retry_SetsAttemptProperty()
    {
        var attempts = new List<int>();
        var call = 0;
        var wf = Workflow.Create("test")
            .Retry(body => body.Step("work", ctx =>
            {
                attempts.Add((int)ctx.Properties["Retry.Attempt"]!);
                call++;
                if (call < 2) throw new Exception();
                return Task.CompletedTask;
            }), 3)
            .Build();
        await wf.ExecuteAsync(new WorkflowContext());
        attempts.Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task While_RespectsCancellation()
    {
        var cts = new CancellationTokenSource();
        var count = 0;
        var wf = Workflow.Create("test")
            .While(ctx => true, body => body.Step("inc", ctx =>
            {
                count++;
                if (count >= 2) cts.Cancel();
                return Task.CompletedTask;
            }))
            .Build();
        var result = await wf.ExecuteAsync(new WorkflowContext(cts.Token));
        result.Status.Should().Be(WorkflowStatus.Aborted);
    }
}
