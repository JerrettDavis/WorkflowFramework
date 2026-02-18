using FluentAssertions;
using WorkflowFramework.Persistence;
using Xunit;

namespace WorkflowFramework.Tests.Core;

public class WorkflowStateCoreTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var state = new WorkflowState();
        state.WorkflowId.Should().BeEmpty();
        state.CorrelationId.Should().BeEmpty();
        state.WorkflowName.Should().BeEmpty();
        state.LastCompletedStepIndex.Should().Be(-1);
        state.Status.Should().Be(WorkflowStatus.Pending);
        state.Properties.Should().BeEmpty();
        state.SerializedData.Should().BeNull();
        state.Timestamp.Should().Be(default);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var ts = DateTimeOffset.UtcNow;
        var state = new WorkflowState
        {
            WorkflowId = "wf1",
            CorrelationId = "cor1",
            WorkflowName = "MyWf",
            LastCompletedStepIndex = 5,
            Status = WorkflowStatus.Completed,
            SerializedData = "{\"key\":1}",
            Timestamp = ts
        };
        state.WorkflowId.Should().Be("wf1");
        state.CorrelationId.Should().Be("cor1");
        state.WorkflowName.Should().Be("MyWf");
        state.LastCompletedStepIndex.Should().Be(5);
        state.Status.Should().Be(WorkflowStatus.Completed);
        state.SerializedData.Should().Be("{\"key\":1}");
        state.Timestamp.Should().Be(ts);
    }

    [Fact]
    public void Properties_Dictionary_CanAdd()
    {
        var state = new WorkflowState();
        state.Properties["key"] = "val";
        state.Properties.Should().ContainKey("key");
    }
}
