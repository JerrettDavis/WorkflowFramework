using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;
using Xunit;

namespace WorkflowFramework.Tests.ExecutionHistory;

public class InMemoryExecutionHistoryStoreTests
{
    private readonly InMemoryExecutionHistoryStore _store = new();

    [Fact]
    public async Task RecordRunAsync_StoresRecord()
    {
        var record = CreateRecord("run-1", "TestWorkflow", WorkflowStatus.Completed);

        await _store.RecordRunAsync(record);

        _store.AllRecords.Should().ContainSingle().Which.RunId.Should().Be("run-1");
    }

    [Fact]
    public async Task GetRunAsync_ReturnsNullForMissing()
    {
        var result = await _store.GetRunAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRunAsync_ReturnsExistingRecord()
    {
        var record = CreateRecord("run-1", "Wf", WorkflowStatus.Completed);
        await _store.RecordRunAsync(record);

        var result = await _store.GetRunAsync("run-1");
        result.Should().BeSameAs(record);
    }

    [Fact]
    public async Task GetRunsAsync_NoFilter_ReturnsAll()
    {
        await _store.RecordRunAsync(CreateRecord("r1", "A", WorkflowStatus.Completed));
        await _store.RecordRunAsync(CreateRecord("r2", "B", WorkflowStatus.Faulted));

        var results = await _store.GetRunsAsync();
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRunsAsync_FilterByWorkflowName()
    {
        await _store.RecordRunAsync(CreateRecord("r1", "Alpha", WorkflowStatus.Completed));
        await _store.RecordRunAsync(CreateRecord("r2", "Beta", WorkflowStatus.Completed));

        var results = await _store.GetRunsAsync(new ExecutionHistoryFilter { WorkflowName = "Alpha" });
        results.Should().ContainSingle().Which.RunId.Should().Be("r1");
    }

    [Fact]
    public async Task GetRunsAsync_FilterByStatus()
    {
        await _store.RecordRunAsync(CreateRecord("r1", "Wf", WorkflowStatus.Completed));
        await _store.RecordRunAsync(CreateRecord("r2", "Wf", WorkflowStatus.Faulted));

        var results = await _store.GetRunsAsync(new ExecutionHistoryFilter { Status = WorkflowStatus.Faulted });
        results.Should().ContainSingle().Which.RunId.Should().Be("r2");
    }

    [Fact]
    public async Task GetRunsAsync_FilterByMaxResults()
    {
        for (var i = 0; i < 5; i++)
            await _store.RecordRunAsync(CreateRecord($"r{i}", "Wf", WorkflowStatus.Completed));

        var results = await _store.GetRunsAsync(new ExecutionHistoryFilter { MaxResults = 2 });
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRunsAsync_FilterByDateRange()
    {
        var now = DateTimeOffset.UtcNow;
        var old = CreateRecord("r1", "Wf", WorkflowStatus.Completed);
        old.StartedAt = now.AddHours(-5);
        var recent = CreateRecord("r2", "Wf", WorkflowStatus.Completed);
        recent.StartedAt = now;

        await _store.RecordRunAsync(old);
        await _store.RecordRunAsync(recent);

        var results = await _store.GetRunsAsync(new ExecutionHistoryFilter { From = now.AddHours(-1) });
        results.Should().ContainSingle().Which.RunId.Should().Be("r2");
    }

    private static WorkflowRunRecord CreateRecord(string runId, string name, WorkflowStatus status) => new()
    {
        RunId = runId,
        WorkflowName = name,
        Status = status,
        StartedAt = DateTimeOffset.UtcNow
    };
}
