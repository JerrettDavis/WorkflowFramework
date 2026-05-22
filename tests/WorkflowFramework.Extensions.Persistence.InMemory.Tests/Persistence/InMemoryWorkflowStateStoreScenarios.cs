using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Persistence.InMemory;
using WorkflowFramework.Persistence;

namespace WorkflowFramework.Extensions.Persistence.InMemory.Tests.Persistence;

[Feature("InMemoryWorkflowStateStore — thread-safe in-memory checkpoint storage")]
public class InMemoryWorkflowStateStoreScenarios : TinyBddXunitBase
{
    public InMemoryWorkflowStateStoreScenarios(ITestOutputHelper output) : base(output) { }

    private static WorkflowState MakeState(string workflowId = "wf-1", int stepIndex = 0) =>
        new()
        {
            WorkflowId = workflowId,
            CorrelationId = "corr-1",
            WorkflowName = "TestWorkflow",
            LastCompletedStepIndex = stepIndex,
            Status = WorkflowStatus.Running,
            Properties = new Dictionary<string, object?> { ["step"] = stepIndex },
            Timestamp = DateTimeOffset.UtcNow
        };

    [Scenario("load returns null for unknown workflow"), Fact]
    public async Task LoadReturnsNullForUnknown()
    {
        var store = new InMemoryWorkflowStateStore();
        var result = await store.LoadCheckpointAsync("unknown");

        await Given("an empty store loaded for unknown id", () => result)
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
        var store = new InMemoryWorkflowStateStore();
        var state = MakeState("wf-roundtrip", stepIndex: 3);
        await store.SaveCheckpointAsync("wf-roundtrip", state);
        var loaded = await store.LoadCheckpointAsync("wf-roundtrip");

        await Given("state saved and loaded for 'wf-roundtrip'", () => loaded)
            .Then("loaded state matches saved state", l =>
            {
                l.Should().NotBeNull();
                l!.WorkflowId.Should().Be("wf-roundtrip");
                l.LastCompletedStepIndex.Should().Be(3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("save overwrites existing checkpoint"), Fact]
    public async Task SaveOverwritesExisting()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveCheckpointAsync("wf-overwrite", MakeState("wf-overwrite", stepIndex: 1));
        await store.SaveCheckpointAsync("wf-overwrite", MakeState("wf-overwrite", stepIndex: 7));
        var loaded = await store.LoadCheckpointAsync("wf-overwrite");

        await Given("second save with step 7 overwriting step 1", () => loaded)
            .Then("loaded step index reflects the latest save", l =>
            {
                l!.LastCompletedStepIndex.Should().Be(7);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("delete removes existing checkpoint"), Fact]
    public async Task DeleteRemovesCheckpoint()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveCheckpointAsync("wf-delete", MakeState("wf-delete"));
        await store.DeleteCheckpointAsync("wf-delete");
        var afterDelete = await store.LoadCheckpointAsync("wf-delete");

        await Given("checkpoint saved then deleted", () => afterDelete)
            .Then("result after delete is null", r =>
            {
                r.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("delete on non-existent key does not throw"), Fact]
    public async Task DeleteNonExistentDoesNotThrow()
    {
        var store = new InMemoryWorkflowStateStore();

        Exception? ex = null;
        try { await store.DeleteCheckpointAsync("non-existent"); }
        catch (Exception e) { ex = e; }

        await Given("delete called on unknown key", () => ex)
            .Then("no exception was thrown", e =>
            {
                e.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("save rejects null state"), Fact]
    public async Task SaveRejectsNullState()
    {
        var store = new InMemoryWorkflowStateStore();

        Exception? ex = null;
        try { await store.SaveCheckpointAsync("wf-null", null!); }
        catch (Exception e) { ex = e; }

        await Given("null state passed to SaveCheckpointAsync", () => ex)
            .Then("ArgumentNullException is thrown", e =>
            {
                e.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("multiple workflows coexist independently"), Fact]
    public async Task MultipleWorkflowsCoexist()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveCheckpointAsync("wf-a", MakeState("wf-a", stepIndex: 1));
        await store.SaveCheckpointAsync("wf-b", MakeState("wf-b", stepIndex: 2));

        var a = await store.LoadCheckpointAsync("wf-a");
        var b = await store.LoadCheckpointAsync("wf-b");

        await Given("two workflows stored with different step indices", () => (a, b))
            .Then("each loads independently with correct values", pair =>
            {
                pair.a!.LastCompletedStepIndex.Should().Be(1);
                pair.b!.LastCompletedStepIndex.Should().Be(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetAllStates returns all stored entries"), Fact]
    public async Task GetAllStatesReturnsAllEntries()
    {
        var store = new InMemoryWorkflowStateStore();
        await store.SaveCheckpointAsync("wf-x", MakeState("wf-x"));
        await store.SaveCheckpointAsync("wf-y", MakeState("wf-y"));

        await Given("two states stored", () => store.GetAllStates())
            .Then("GetAllStates contains both workflow ids", all =>
            {
                all.Should().ContainKey("wf-x").And.ContainKey("wf-y");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("cancellation token is accepted without error"), Fact]
    public async Task CancellationTokenAccepted()
    {
        var store = new InMemoryWorkflowStateStore();
        using var cts = new CancellationTokenSource();
        await store.SaveCheckpointAsync("wf-ct", MakeState("wf-ct"), cts.Token);
        var result = await store.LoadCheckpointAsync("wf-ct", cts.Token);

        await Given("state saved and loaded with explicit cancellation token", () => result)
            .Then("result is not null", r =>
            {
                r.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
