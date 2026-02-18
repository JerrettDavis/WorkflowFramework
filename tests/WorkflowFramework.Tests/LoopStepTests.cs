using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class LoopStepTests
{
    [Fact]
    public async Task ForEach_IteratesOverCollection()
    {
        var workflow = Workflow.Create()
            .Step("Init", ctx =>
            {
                ctx.Properties["Items"] = new List<string> { "A", "B", "C" };
                ctx.Properties["Results"] = new List<string>();
                return Task.CompletedTask;
            })
            .ForEach<string>(
                ctx => (List<string>)ctx.Properties["Items"]!,
                b => b.Step("Process", ctx =>
                {
                    var current = (string)ctx.Properties["ForEach.Current"]!;
                    ((List<string>)ctx.Properties["Results"]!).Add(current);
                    return Task.CompletedTask;
                }))
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        ((List<string>)context.Properties["Results"]!).Should().BeEquivalentTo("A", "B", "C");
    }

    [Fact]
    public async Task While_LoopsUntilConditionFalse()
    {
        var workflow = Workflow.Create()
            .Step("Init", ctx =>
            {
                ctx.Properties["Counter"] = 0;
                return Task.CompletedTask;
            })
            .While(
                ctx => (int)ctx.Properties["Counter"]! < 3,
                b => b.Step("Increment", ctx =>
                {
                    ctx.Properties["Counter"] = (int)ctx.Properties["Counter"]! + 1;
                    return Task.CompletedTask;
                }))
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        ((int)context.Properties["Counter"]!).Should().Be(3);
    }

    [Fact]
    public async Task DoWhile_ExecutesAtLeastOnce()
    {
        var workflow = Workflow.Create()
            .Step("Init", ctx =>
            {
                ctx.Properties["Counter"] = 0;
                return Task.CompletedTask;
            })
            .DoWhile(
                b => b.Step("Increment", ctx =>
                {
                    ctx.Properties["Counter"] = (int)ctx.Properties["Counter"]! + 1;
                    return Task.CompletedTask;
                }),
                ctx => false) // Condition is false immediately, but body runs once
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        ((int)context.Properties["Counter"]!).Should().Be(1);
    }

    [Fact]
    public async Task Retry_RetriesOnFailure()
    {
        var attemptCount = 0;

        var workflow = Workflow.Create()
            .Retry(b => b.Step("Flaky", ctx =>
            {
                attemptCount++;
                if (attemptCount < 3)
                    throw new InvalidOperationException("Flaky!");
                ctx.Properties["Success"] = true;
                return Task.CompletedTask;
            }), maxAttempts: 3)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        attemptCount.Should().Be(3);
        ((bool)context.Properties["Success"]!).Should().BeTrue();
    }
}
