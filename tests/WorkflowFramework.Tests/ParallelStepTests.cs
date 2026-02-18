using FluentAssertions;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class ParallelStepTests
{
    [Fact]
    public async Task Given_ParallelSteps_When_Executed_Then_AllRun()
    {
        // Given
        var workflow = Workflow.Create()
            .Parallel(p => p
                .Step(new TrackingStep("P1"))
                .Step(new TrackingStep("P2"))
                .Step(new TrackingStep("P3")))
            .Build();

        var context = new WorkflowContext();

        // When
        await workflow.ExecuteAsync(context);

        // Then
        var log = TrackingStep.GetLog(context);
        log.Should().HaveCount(3);
        log.Should().Contain("P1");
        log.Should().Contain("P2");
        log.Should().Contain("P3");
    }
}
