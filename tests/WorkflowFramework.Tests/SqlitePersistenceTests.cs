using FluentAssertions;
using WorkflowFramework.Extensions.Persistence.Sqlite;
using WorkflowFramework.Persistence;
using Xunit;

namespace WorkflowFramework.Tests;

public class SqlitePersistenceTests : IDisposable
{
    private readonly SqliteWorkflowStateStore _store;

    public SqlitePersistenceTests()
    {
        _store = new SqliteWorkflowStateStore("Data Source=:memory:");
    }

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var state = new WorkflowState
        {
            WorkflowId = "wf-1",
            CorrelationId = "cor-1",
            WorkflowName = "TestWf",
            LastCompletedStepIndex = 2,
            Status = WorkflowStatus.Running,
            Properties = new Dictionary<string, object?> { ["key"] = "value" },
            Timestamp = DateTimeOffset.UtcNow
        };

        await _store.SaveCheckpointAsync("wf-1", state);
        var loaded = await _store.LoadCheckpointAsync("wf-1");

        loaded.Should().NotBeNull();
        loaded!.WorkflowId.Should().Be("wf-1");
        loaded.CorrelationId.Should().Be("cor-1");
        loaded.WorkflowName.Should().Be("TestWf");
        loaded.LastCompletedStepIndex.Should().Be(2);
        loaded.Status.Should().Be(WorkflowStatus.Running);
    }

    [Fact]
    public async Task Load_NonExistent_ReturnsNull()
    {
        var loaded = await _store.LoadCheckpointAsync("nonexistent");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Delete_RemovesState()
    {
        var state = new WorkflowState
        {
            WorkflowId = "wf-del",
            CorrelationId = "c",
            WorkflowName = "T",
            Timestamp = DateTimeOffset.UtcNow
        };
        await _store.SaveCheckpointAsync("wf-del", state);
        await _store.DeleteCheckpointAsync("wf-del");

        var loaded = await _store.LoadCheckpointAsync("wf-del");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Save_Overwrites_ExistingState()
    {
        var state1 = new WorkflowState
        {
            WorkflowId = "wf-up",
            CorrelationId = "c",
            WorkflowName = "T",
            LastCompletedStepIndex = 0,
            Timestamp = DateTimeOffset.UtcNow
        };
        await _store.SaveCheckpointAsync("wf-up", state1);

        var state2 = new WorkflowState
        {
            WorkflowId = "wf-up",
            CorrelationId = "c",
            WorkflowName = "T",
            LastCompletedStepIndex = 5,
            Timestamp = DateTimeOffset.UtcNow
        };
        await _store.SaveCheckpointAsync("wf-up", state2);

        var loaded = await _store.LoadCheckpointAsync("wf-up");
        loaded!.LastCompletedStepIndex.Should().Be(5);
    }

    [Fact]
    public async Task SaveWithSerializedData_RoundTrips()
    {
        var state = new WorkflowState
        {
            WorkflowId = "wf-sd",
            CorrelationId = "c",
            WorkflowName = "T",
            SerializedData = "{\"name\":\"test\"}",
            Timestamp = DateTimeOffset.UtcNow
        };
        await _store.SaveCheckpointAsync("wf-sd", state);

        var loaded = await _store.LoadCheckpointAsync("wf-sd");
        loaded!.SerializedData.Should().Be("{\"name\":\"test\"}");
    }
}
