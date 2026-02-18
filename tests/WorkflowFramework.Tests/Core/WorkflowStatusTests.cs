using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowStatusTests
{
    [Theory]
    [InlineData(WorkflowStatus.Pending, 0)]
    [InlineData(WorkflowStatus.Running, 1)]
    [InlineData(WorkflowStatus.Completed, 2)]
    [InlineData(WorkflowStatus.Faulted, 3)]
    [InlineData(WorkflowStatus.Aborted, 4)]
    [InlineData(WorkflowStatus.Compensated, 5)]
    [InlineData(WorkflowStatus.Suspended, 6)]
    public void EnumValues_AreCorrect(WorkflowStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    [Fact]
    public void AllValues_Count()
    {
        Enum.GetValues<WorkflowStatus>().Should().HaveCount(7);
    }
}
