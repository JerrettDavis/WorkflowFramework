using FluentAssertions;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class ConditionalStepTests
{
    [Fact]
    public async Task Given_TrueCondition_When_Executed_Then_ThenBranchRuns()
    {
        // Given
        var workflow = Workflow.Create()
            .If(_ => true).Then(new TrackingStep("Then")).Else(new TrackingStep("Else"))
            .Build();

        var context = new WorkflowContext();

        // When
        await workflow.ExecuteAsync(context);

        // Then
        TrackingStep.GetLog(context).Should().ContainSingle().Which.Should().Be("Then");
    }

    [Fact]
    public async Task Given_FalseCondition_When_Executed_Then_ElseBranchRuns()
    {
        // Given
        var workflow = Workflow.Create()
            .If(_ => false).Then(new TrackingStep("Then")).Else(new TrackingStep("Else"))
            .Build();

        var context = new WorkflowContext();

        // When
        await workflow.ExecuteAsync(context);

        // Then
        TrackingStep.GetLog(context).Should().ContainSingle().Which.Should().Be("Else");
    }

    [Fact]
    public async Task Given_FalseConditionWithoutElse_When_Executed_Then_NothingRuns()
    {
        // Given
        var workflow = Workflow.Create()
            .If(_ => false).Then(new TrackingStep("Then")).EndIf()
            .Build();

        var context = new WorkflowContext();

        // When
        await workflow.ExecuteAsync(context);

        // Then
        TrackingStep.GetLog(context).Should().BeEmpty();
    }
}
