using FluentAssertions;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class CompensationTests
{
    [Fact]
    public async Task Given_CompensationEnabled_When_StepFails_Then_PreviousStepsCompensated()
    {
        // Given
        var workflow = Workflow.Create()
            .WithCompensation()
            .Step(new CompensatingTrackingStep("S1"))
            .Step(new CompensatingTrackingStep("S2"))
            .Step(new FailingStep())
            .Build();

        var context = new WorkflowContext();

        // When
        var result = await workflow.ExecuteAsync(context);

        // Then
        result.Status.Should().Be(WorkflowStatus.Compensated);
        var log = TrackingStep.GetLog(context);
        log.Should().Contain("S1:Execute");
        log.Should().Contain("S2:Execute");
        log.Should().Contain("S2:Compensate");
        log.Should().Contain("S1:Compensate");

        // Compensation should be in reverse order
        var s2CompIdx = log.IndexOf("S2:Compensate");
        var s1CompIdx = log.IndexOf("S1:Compensate");
        s2CompIdx.Should().BeLessThan(s1CompIdx);
    }

    [Fact]
    public async Task Given_NoCompensation_When_StepFails_Then_Faulted()
    {
        // Given
        var workflow = Workflow.Create()
            .Step(new CompensatingTrackingStep("S1"))
            .Step(new FailingStep())
            .Build();

        var context = new WorkflowContext();

        // When
        var result = await workflow.ExecuteAsync(context);

        // Then
        result.Status.Should().Be(WorkflowStatus.Faulted);
        result.Errors.Should().HaveCount(1);
    }
}
