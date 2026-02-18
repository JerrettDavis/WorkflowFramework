using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class DiagnosticsTests
{
    [Fact]
    public async Task Given_TimingMiddleware_When_StepsComplete_Then_TimingsRecorded()
    {
        // Given
        var workflow = Workflow.Create()
            .Use(new TimingMiddleware())
            .Step(new TrackingStep("S1"))
            .Step(new TrackingStep("S2"))
            .Build();

        var context = new WorkflowContext();

        // When
        await workflow.ExecuteAsync(context);

        // Then
        context.Properties.Should().ContainKey(TimingMiddleware.TimingsKey);
        var timings = (Dictionary<string, TimeSpan>)context.Properties[TimingMiddleware.TimingsKey]!;
        timings.Should().ContainKeys("S1", "S2");
        timings["S1"].Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }
}
