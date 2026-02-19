using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Extensions.Persistence.EntityFramework;
using WorkflowFramework.Persistence;
using Xunit;

namespace WorkflowFramework.Tests.Persistence;

public class EfCoreStateStoreTests : IDisposable
{
    private readonly WorkflowDbContext _context;
    private readonly EfCoreWorkflowStateStore _store;

    public EfCoreStateStoreTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new WorkflowDbContext(options);
        _context.Database.EnsureCreated();
        _store = new EfCoreWorkflowStateStore(_context);
    }

    [Fact]
    public void Constructor_NullContext_Throws()
    {
        var act = () => new EfCoreWorkflowStateStore(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip()
    {
        var state = CreateState("wf1", 2);
        await _store.SaveCheckpointAsync("wf1", state);
        var loaded = await _store.LoadCheckpointAsync("wf1");
        loaded.Should().NotBeNull();
        loaded!.WorkflowId.Should().Be("wf1");
        loaded.LastCompletedStepIndex.Should().Be(2);
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
    public async Task Save_Update_OverwritesExisting()
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
    public void WorkflowStateEntity_DefaultValues()
    {
        var entity = new WorkflowStateEntity();
        entity.WorkflowId.Should().BeEmpty();
        entity.CorrelationId.Should().BeEmpty();
        entity.WorkflowName.Should().BeEmpty();
        entity.LastCompletedStepIndex.Should().Be(-1);
        entity.Status.Should().Be(0);
        entity.PropertiesJson.Should().BeNull();
        entity.SerializedData.Should().BeNull();
    }

    public void Dispose() => _context.Dispose();

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
