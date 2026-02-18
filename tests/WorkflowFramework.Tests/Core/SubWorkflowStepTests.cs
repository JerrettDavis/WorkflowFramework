using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class SubWorkflowStepCoreTests
{
    [Fact]
    public async Task SubWorkflow_ExecutesChildWorkflow()
    {
        var sub = Workflow.Create("sub").Step(new TrackingStep("SubStep")).Build();
        var wf = Workflow.Create("parent")
            .SubWorkflow(sub)
            .Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        TrackingStep.GetLog(ctx).Should().Contain("SubStep");
    }

    [Fact]
    public async Task SubWorkflow_ChildFails_AbortsParent()
    {
        var sub = Workflow.Create("sub").Step(new FailingStep()).Build();
        var wf = Workflow.Create("parent")
            .SubWorkflow(sub)
            .Step(new TrackingStep("AfterSub"))
            .Build();
        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);
        // Sub-workflow failure sets IsAborted, so parent aborts
        result.Status.Should().Be(WorkflowStatus.Aborted);
        TrackingStep.GetLog(ctx).Should().NotContain("AfterSub");
    }

    [Fact]
    public async Task SubWorkflow_PropagatesContext()
    {
        var sub = Workflow.Create("sub")
            .Step("setprop", ctx => { ctx.Properties["from_sub"] = true; return Task.CompletedTask; })
            .Build();
        var wf = Workflow.Create("parent").SubWorkflow(sub).Build();
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        ctx.Properties["from_sub"].Should().Be(true);
    }
}
