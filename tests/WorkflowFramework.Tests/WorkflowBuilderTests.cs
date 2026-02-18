using FluentAssertions;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class WorkflowBuilderTests
{
    // Given a workflow with multiple steps
    // When the workflow is executed
    // Then all steps execute in order

    [Fact]
    public async Task Given_MultipleSteps_When_Executed_Then_AllRunInOrder()
    {
        // Given
        var workflow = Workflow.Create("test")
            .Step(new TrackingStep("A"))
            .Step(new TrackingStep("B"))
            .Step(new TrackingStep("C"))
            .Build();

        var context = new WorkflowContext();

        // When
        var result = await workflow.ExecuteAsync(context);

        // Then
        result.IsSuccess.Should().BeTrue();
        TrackingStep.GetLog(context).Should().ContainInOrder("A", "B", "C");
    }

    [Fact]
    public async Task Given_InlineStep_When_Executed_Then_DelegateRuns()
    {
        // Given
        var executed = false;
        var workflow = Workflow.Create()
            .Step("inline", _ => { executed = true; return Task.CompletedTask; })
            .Build();

        // When
        await workflow.ExecuteAsync(new WorkflowContext());

        // Then
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Given_EmptyWorkflow_When_Executed_Then_Completes()
    {
        // Given
        var workflow = Workflow.Create().Build();

        // When
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        // Then
        result.Status.Should().Be(WorkflowStatus.Completed);
    }
}
