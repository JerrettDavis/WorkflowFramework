using FluentAssertions;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowEventsBaseTests
{
    private class TestEventsImpl : WorkflowEventsBase { }

    [Fact]
    public async Task AllDefaultMethods_ReturnCompletedTask()
    {
        var events = new TestEventsImpl();
        var ctx = new WorkflowContext();
        var step = new TrackingStep();
        var ex = new Exception();

        await events.OnWorkflowStartedAsync(ctx);
        await events.OnWorkflowCompletedAsync(ctx);
        await events.OnWorkflowFailedAsync(ctx, ex);
        await events.OnStepStartedAsync(ctx, step);
        await events.OnStepCompletedAsync(ctx, step);
        await events.OnStepFailedAsync(ctx, step, ex);
        // No exceptions = pass
    }
}
