using FluentAssertions;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class EventTests
{
    private class TrackingEvents : WorkflowEventsBase
    {
        public List<string> Log { get; } = new();

        public override Task OnWorkflowStartedAsync(IWorkflowContext context)
        {
            Log.Add("WorkflowStarted");
            return Task.CompletedTask;
        }

        public override Task OnWorkflowCompletedAsync(IWorkflowContext context)
        {
            Log.Add("WorkflowCompleted");
            return Task.CompletedTask;
        }

        public override Task OnStepStartedAsync(IWorkflowContext context, IStep step)
        {
            Log.Add($"StepStarted:{step.Name}");
            return Task.CompletedTask;
        }

        public override Task OnStepCompletedAsync(IWorkflowContext context, IStep step)
        {
            Log.Add($"StepCompleted:{step.Name}");
            return Task.CompletedTask;
        }

        public override Task OnWorkflowFailedAsync(IWorkflowContext context, Exception exception)
        {
            Log.Add("WorkflowFailed");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Given_EventHandler_When_WorkflowCompletes_Then_AllEventsRaised()
    {
        // Given
        var events = new TrackingEvents();
        var workflow = Workflow.Create()
            .WithEvents(events)
            .Step(new TrackingStep("S1"))
            .Build();

        // When
        await workflow.ExecuteAsync(new WorkflowContext());

        // Then
        events.Log.Should().ContainInOrder(
            "WorkflowStarted",
            "StepStarted:S1",
            "StepCompleted:S1",
            "WorkflowCompleted");
    }

    [Fact]
    public async Task Given_EventHandler_When_StepFails_Then_FailureEventRaised()
    {
        // Given
        var events = new TrackingEvents();
        var workflow = Workflow.Create()
            .WithEvents(events)
            .Step(new FailingStep())
            .Build();

        // When
        await workflow.ExecuteAsync(new WorkflowContext());

        // Then
        events.Log.Should().Contain("WorkflowFailed");
    }
}
