using FluentAssertions;
using WorkflowFramework.Extensions.Persistence.InMemory;
using WorkflowFramework.Persistence;
using Xunit;

namespace WorkflowFramework.Tests.Persistence;

public class InMemoryStateStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTrip()
    {
        var store = new InMemoryWorkflowStateStore();
        var state = CreateState("wf1");
        await store.SaveCheckpointAsync("wf1", state);
        var loaded = await store.LoadCheckpointAsync("wf1");
        loaded.Should().NotBeNull();
        loaded!.WorkflowId.Should().Be("wf1");
    }

    [Fact]
    public async Task Load_Missing_ReturnsNull()
    {
        var store = new InMemoryWorkflowStateStore();
        var loaded = await store.LoadCheckpointAsync("nonexistent");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Save_NullState_Throws()
    {
        var store = new InMemoryWorkflowStateStore();
        var act = () => store.SaveCheckpointAsync("wf1", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Delete_RemovesState()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveCheckpointAsync("wf1", CreateState("wf1"));
        await store.DeleteCheckpointAsync("wf1");
        var loaded = await store.LoadCheckpointAsync("wf1");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Delete_NonExistent_DoesNotThrow()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.DeleteCheckpointAsync("nonexistent"); // Should not throw
    }

    [Fact]
    public async Task Save_OverwritesExisting()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveCheckpointAsync("wf1", CreateState("wf1", 1));
        await store.SaveCheckpointAsync("wf1", CreateState("wf1", 5));
        var loaded = await store.LoadCheckpointAsync("wf1");
        loaded!.LastCompletedStepIndex.Should().Be(5);
    }

    [Fact]
    public async Task GetAllStates_ReturnsAll()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveCheckpointAsync("a", CreateState("a"));
        await store.SaveCheckpointAsync("b", CreateState("b"));
        store.GetAllStates().Should().HaveCount(2);
    }

    [Fact]
    public async Task ConcurrentAccess_DoesNotThrow()
    {
        var store = new InMemoryWorkflowStateStore();
        var tasks = Enumerable.Range(0, 20).Select(i =>
            store.SaveCheckpointAsync($"wf{i}", CreateState($"wf{i}")));
        await Task.WhenAll(tasks);
        store.GetAllStates().Should().HaveCount(20);
    }

    private static WorkflowState CreateState(string id, int stepIndex = 0) => new()
    {
        WorkflowId = id,
        CorrelationId = "corr",
        WorkflowName = "Test",
        LastCompletedStepIndex = stepIndex,
        Status = WorkflowStatus.Running,
        Timestamp = DateTimeOffset.UtcNow
    };
}
