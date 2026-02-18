using FluentAssertions;
using WorkflowFramework.Builder;
using Xunit;

namespace WorkflowFramework.Tests;

public class TryCatchTests
{
    [Fact]
    public async Task Try_Catch_HandlesException()
    {
        var caught = false;

        var workflow = Workflow.Create()
            .Try(b => b.Step("Fail", _ => throw new InvalidOperationException("Boom!")))
            .Catch<InvalidOperationException>((ctx, ex) =>
            {
                caught = true;
                ctx.Properties["Error"] = ex.Message;
                return Task.CompletedTask;
            })
            .EndTry()
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        caught.Should().BeTrue();
        context.Properties["Error"].Should().Be("Boom!");
    }

    [Fact]
    public async Task Try_Finally_AlwaysRuns()
    {
        var finallyRan = false;

        var workflow = Workflow.Create()
            .Try(b => b.Step("Work", ctx =>
            {
                ctx.Properties["Worked"] = true;
                return Task.CompletedTask;
            }))
            .Catch<Exception>((_, _) => Task.CompletedTask)
            .Finally(b => b.Step("Cleanup", ctx =>
            {
                finallyRan = true;
                return Task.CompletedTask;
            }))
            .Build();

        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);

        finallyRan.Should().BeTrue();
    }

    [Fact]
    public async Task Try_Catch_Finally_AllExecute()
    {
        var caughtMessage = "";
        var finallyRan = false;

        var workflow = Workflow.Create()
            .Try(b => b.Step("Fail", _ => throw new ArgumentException("Bad arg")))
            .Catch<ArgumentException>((ctx, ex) =>
            {
                caughtMessage = ex.Message;
                return Task.CompletedTask;
            })
            .Finally(b => b.Step("Cleanup", _ =>
            {
                finallyRan = true;
                return Task.CompletedTask;
            }))
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        caughtMessage.Should().Be("Bad arg");
        finallyRan.Should().BeTrue();
    }
}
