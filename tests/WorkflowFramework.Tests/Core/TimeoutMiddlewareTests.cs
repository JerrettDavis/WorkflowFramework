using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class TimeoutMiddlewareTests
{
    [Fact]
    public async Task WithTimeout_StepCompletes_Succeeds()
    {
        var wf = Workflow.Create("test")
            .Step(new TrackingStep("A"))
            .WithTimeout(TimeSpan.FromSeconds(10))
            .Build();
        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Delay_Step_Works()
    {
        var wf = Workflow.Create("test")
            .Delay(TimeSpan.FromMilliseconds(1))
            .Build();
        var result = await wf.ExecuteAsync(new WorkflowContext());
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Delay_RespectsCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var wf = Workflow.Create("test")
            .Delay(TimeSpan.FromHours(1))
            .Build();
        var result = await wf.ExecuteAsync(new WorkflowContext(cts.Token));
        result.Status.Should().Be(WorkflowStatus.Aborted);
    }
}
