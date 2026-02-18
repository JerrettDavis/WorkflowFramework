using FluentAssertions;
using WorkflowFramework.Extensions.Persistence;
using WorkflowFramework.Extensions.Persistence.InMemory;
using WorkflowFramework.Persistence;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task Given_CheckpointMiddleware_When_StepsComplete_Then_StateIsSaved()
    {
        // Given
        var store = new InMemoryWorkflowStateStore();
        var workflow = Workflow.Create()
            .Use(new CheckpointMiddleware(store))
            .Step(new TrackingStep("S1"))
            .Step(new TrackingStep("S2"))
            .Build();

        var context = new WorkflowContext();

        // When
        await workflow.ExecuteAsync(context);

        // Then
        var state = await store.LoadCheckpointAsync(context.WorkflowId);
        state.Should().NotBeNull();
        state!.LastCompletedStepIndex.Should().Be(1);
    }

    [Fact]
    public async Task Given_InMemoryStore_When_DeleteCheckpoint_Then_StateRemoved()
    {
        // Given
        var store = new InMemoryWorkflowStateStore();
        var state = new WorkflowState
        {
            WorkflowId = "test-1",
            Status = WorkflowStatus.Running,
            Timestamp = DateTimeOffset.UtcNow
        };
        await store.SaveCheckpointAsync("test-1", state);

        // When
        await store.DeleteCheckpointAsync("test-1");

        // Then
        var loaded = await store.LoadCheckpointAsync("test-1");
        loaded.Should().BeNull();
    }
}
