using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Persistence.Sqlite;
using WorkflowFramework.Persistence;

namespace WorkflowFramework.Extensions.Persistence.Sqlite.Tests.Persistence;

[Feature("SqliteWorkflowStateStore — durable checkpoint storage via SQLite")]
public class SqliteWorkflowStateStoreScenarios : TinyBddXunitBase
{
    public SqliteWorkflowStateStoreScenarios(ITestOutputHelper output) : base(output) { }

    // Each test uses :memory: for a clean, isolated DB without Docker.
    private static SqliteWorkflowStateStore CreateStore() =>
        new("Data Source=:memory:");

    private static WorkflowState MakeState(string workflowId = "wf-1", int stepIndex = 0) =>
        new()
        {
            WorkflowId = workflowId,
            CorrelationId = "corr-sqlite",
            WorkflowName = "SqliteTest",
            LastCompletedStepIndex = stepIndex,
            Status = WorkflowStatus.Running,
            Properties = new Dictionary<string, object?> { ["env"] = "test" },
            Timestamp = DateTimeOffset.UtcNow
        };

    [Scenario("load returns null for unknown workflow"), Fact]
    public async Task LoadReturnsNullForUnknown()
    {
        using var store = CreateStore();
        var result = await store.LoadCheckpointAsync("unknown");

        await Given("empty SQLite store loaded for unknown id", () => result)
            .Then("result is null", r =>
            {
                r.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("save and load round-trip preserves state"), Fact]
    public async Task SaveAndLoadRoundTrip()
    {
        using var store = CreateStore();
        await store.SaveCheckpointAsync("wf-sqlite", MakeState("wf-sqlite", stepIndex: 4));
        var loaded = await store.LoadCheckpointAsync("wf-sqlite");

        await Given("state saved to SQLite then loaded", () => loaded)
            .Then("loaded state has correct workflowId and step index", l =>
            {
                l.Should().NotBeNull();
                l!.WorkflowId.Should().Be("wf-sqlite");
                l.LastCompletedStepIndex.Should().Be(4);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("save overwrites existing checkpoint via upsert"), Fact]
    public async Task SaveOverwritesExisting()
    {
        using var store = CreateStore();
        await store.SaveCheckpointAsync("wf-upsert", MakeState("wf-upsert", stepIndex: 1));
        await store.SaveCheckpointAsync("wf-upsert", MakeState("wf-upsert", stepIndex: 9));
        var loaded = await store.LoadCheckpointAsync("wf-upsert");

        await Given("checkpoint saved twice for same workflowId", () => loaded)
            .Then("loaded step index is from the second save", l =>
            {
                l!.LastCompletedStepIndex.Should().Be(9);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("delete removes existing checkpoint"), Fact]
    public async Task DeleteRemovesCheckpoint()
    {
        using var store = CreateStore();
        await store.SaveCheckpointAsync("wf-del", MakeState("wf-del"));
        await store.DeleteCheckpointAsync("wf-del");
        var afterDelete = await store.LoadCheckpointAsync("wf-del");

        await Given("checkpoint saved then deleted", () => afterDelete)
            .Then("checkpoint no longer exists", r =>
            {
                r.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("delete on non-existent key does not throw"), Fact]
    public async Task DeleteNonExistentDoesNotThrow()
    {
        using var store = CreateStore();

        Exception? ex = null;
        try { await store.DeleteCheckpointAsync("ghost"); }
        catch (Exception e) { ex = e; }

        await Given("delete called for unknown id on SQLite store", () => ex)
            .Then("no exception was thrown", e =>
            {
                e.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("status enum value is persisted and restored correctly"), Fact]
    public async Task StatusIsPersisted()
    {
        using var store = CreateStore();
        var state = MakeState("wf-status");
        state.Status = WorkflowStatus.Suspended;

        await store.SaveCheckpointAsync("wf-status", state);
        var loaded = await store.LoadCheckpointAsync("wf-status");

        await Given("state with Suspended status saved to SQLite", () => loaded)
            .Then("loaded status is Suspended", l =>
            {
                l!.Status.Should().Be(WorkflowStatus.Suspended);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("properties dictionary is serialized to JSON and restored"), Fact]
    public async Task PropertiesAreSerializedAsJson()
    {
        using var store = CreateStore();
        var state = MakeState("wf-props");
        state.Properties["custom"] = "value42";

        await store.SaveCheckpointAsync("wf-props", state);
        var loaded = await store.LoadCheckpointAsync("wf-props");

        await Given("state with custom property saved to SQLite", () => loaded)
            .Then("loaded properties contain 'custom' key", l =>
            {
                l!.Properties.Should().ContainKey("custom");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("null serialized data round-trips correctly"), Fact]
    public async Task NullSerializedDataRoundTrips()
    {
        using var store = CreateStore();
        var state = MakeState("wf-nulldata");
        state.SerializedData = null;

        await store.SaveCheckpointAsync("wf-nulldata", state);
        var loaded = await store.LoadCheckpointAsync("wf-nulldata");

        await Given("state with null SerializedData saved to SQLite", () => loaded)
            .Then("loaded SerializedData is null", l =>
            {
                l!.SerializedData.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("non-null serialized data round-trips correctly"), Fact]
    public async Task NonNullSerializedDataRoundTrips()
    {
        using var store = CreateStore();
        var state = MakeState("wf-data");
        state.SerializedData = "{\"amount\":100}";

        await store.SaveCheckpointAsync("wf-data", state);
        var loaded = await store.LoadCheckpointAsync("wf-data");

        await Given("state with JSON SerializedData saved to SQLite", () => loaded)
            .Then("loaded SerializedData matches original", l =>
            {
                l!.SerializedData.Should().Be("{\"amount\":100}");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Dispose closes the SQLite connection cleanly"), Fact]
    public async Task DisposeDoesNotThrow()
    {
        Exception? ex = null;
        try { using var store = CreateStore(); }
        catch (Exception e) { ex = e; }

        await Given("SqliteWorkflowStateStore disposed", () => ex)
            .Then("no exception was thrown", e =>
            {
                e.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }
}
