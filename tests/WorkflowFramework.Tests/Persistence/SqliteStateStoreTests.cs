using FluentAssertions;
using WorkflowFramework.Extensions.Persistence.Sqlite;
using WorkflowFramework.Persistence;
using Xunit;

namespace WorkflowFramework.Tests.Persistence;

public class SqliteStateStoreTests : IDisposable
{
    private readonly SqliteWorkflowStateStore _store;

    public SqliteStateStoreTests()
    {
        _store = new SqliteWorkflowStateStore("Data Source=:memory:");
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip()
    {
        var state = CreateState("wf1", 3);
        await _store.SaveCheckpointAsync("wf1", state);
        var loaded = await _store.LoadCheckpointAsync("wf1");
        loaded.Should().NotBeNull();
        loaded!.WorkflowId.Should().Be("wf1");
        loaded.LastCompletedStepIndex.Should().Be(3);
        loaded.Status.Should().Be(WorkflowStatus.Running);
    }

    [Fact]
    public async Task Load_Missing_ReturnsNull()
    {
        var loaded = await _store.LoadCheckpointAsync("nonexistent");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Delete_RemovesState()
    {
        await _store.SaveCheckpointAsync("wf1", CreateState("wf1"));
        await _store.DeleteCheckpointAsync("wf1");
        var loaded = await _store.LoadCheckpointAsync("wf1");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Delete_NonExistent_DoesNotThrow()
    {
        await _store.DeleteCheckpointAsync("nonexistent");
    }

    [Fact]
    public async Task Save_OverwritesExisting()
    {
        await _store.SaveCheckpointAsync("wf1", CreateState("wf1", 1));
        await _store.SaveCheckpointAsync("wf1", CreateState("wf1", 5));
        var loaded = await _store.LoadCheckpointAsync("wf1");
        loaded!.LastCompletedStepIndex.Should().Be(5);
    }

    [Fact]
    public async Task Save_WithProperties_RoundTrips()
    {
        var state = CreateState("wf1");
        state.Properties["key"] = "value";
        await _store.SaveCheckpointAsync("wf1", state);
        var loaded = await _store.LoadCheckpointAsync("wf1");
        loaded!.Properties.Should().ContainKey("key");
    }

    [Fact]
    public async Task Save_WithSerializedData_RoundTrips()
    {
        var state = CreateState("wf1");
        state.SerializedData = "{\"foo\":\"bar\"}";
        await _store.SaveCheckpointAsync("wf1", state);
        var loaded = await _store.LoadCheckpointAsync("wf1");
        loaded!.SerializedData.Should().Be("{\"foo\":\"bar\"}");
    }

    [Fact]
    public async Task Save_NullSerializedData_RoundTrips()
    {
        var state = CreateState("wf1");
        state.SerializedData = null;
        await _store.SaveCheckpointAsync("wf1", state);
        var loaded = await _store.LoadCheckpointAsync("wf1");
        loaded!.SerializedData.Should().BeNull();
    }

    public void Dispose() => _store.Dispose();

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
