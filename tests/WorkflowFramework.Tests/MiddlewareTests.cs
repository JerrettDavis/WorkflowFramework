using FluentAssertions;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class MiddlewareTests
{
    private class OrderTrackingMiddleware(string name) : IWorkflowMiddleware
    {
        public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
        {
            TrackingStep.GetLog(context).Add($"{name}:Before");
            await next(context);
            TrackingStep.GetLog(context).Add($"{name}:After");
        }
    }

    [Fact]
    public async Task Given_Middleware_When_Executed_Then_WrapsStepExecution()
    {
        // Given
        var workflow = Workflow.Create()
            .Use(new OrderTrackingMiddleware("M1"))
            .Step(new TrackingStep("Step"))
            .Build();

        var context = new WorkflowContext();

        // When
        await workflow.ExecuteAsync(context);

        // Then
        TrackingStep.GetLog(context).Should().ContainInOrder(
            "M1:Before", "Step", "M1:After");
    }

    [Fact]
    public async Task Given_MultipleMiddleware_When_Executed_Then_NestsCorrectly()
    {
        // Given
        var workflow = Workflow.Create()
            .Use(new OrderTrackingMiddleware("Outer"))
            .Use(new OrderTrackingMiddleware("Inner"))
            .Step(new TrackingStep("Step"))
            .Build();

        var context = new WorkflowContext();

        // When
        await workflow.ExecuteAsync(context);

        // Then
        TrackingStep.GetLog(context).Should().ContainInOrder(
            "Outer:Before", "Inner:Before", "Step", "Inner:After", "Outer:After");
    }
}
