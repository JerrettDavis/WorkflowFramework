using FluentAssertions;
using WorkflowFramework.Builder;
using Xunit;

namespace WorkflowFramework.Tests;

public class SubWorkflowTests
{
    [Fact]
    public async Task SubWorkflow_ExecutesChildWorkflow()
    {
        var child = Workflow.Create("Child")
            .Step("ChildStep", ctx =>
            {
                ctx.Properties["ChildRan"] = true;
                return Task.CompletedTask;
            })
            .Build();

        var parent = Workflow.Create("Parent")
            .Step("ParentBefore", ctx =>
            {
                ctx.Properties["Before"] = true;
                return Task.CompletedTask;
            })
            .SubWorkflow(child)
            .Step("ParentAfter", ctx =>
            {
                ctx.Properties["After"] = true;
                return Task.CompletedTask;
            })
            .Build();

        var context = new WorkflowContext();
        var result = await parent.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        context.Properties["Before"].Should().Be(true);
        context.Properties["ChildRan"].Should().Be(true);
        context.Properties["After"].Should().Be(true);
    }

    [Fact]
    public async Task Delay_WaitsSpecifiedTime()
    {
        var workflow = Workflow.Create()
            .Delay(TimeSpan.FromMilliseconds(50))
            .Build();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await workflow.ExecuteAsync(new WorkflowContext());
        sw.Stop();

        result.IsSuccess.Should().BeTrue();
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(40);
    }
}
