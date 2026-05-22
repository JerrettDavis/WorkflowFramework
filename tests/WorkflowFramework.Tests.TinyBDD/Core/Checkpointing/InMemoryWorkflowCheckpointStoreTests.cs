using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Checkpointing;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Core.Checkpointing;

[Feature("InMemory checkpoint store")]
public class InMemoryWorkflowCheckpointStoreTests : TinyBddTestBase
{
    public InMemoryWorkflowCheckpointStoreTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Round-trip save and load returns matching data"), Fact]
    public async Task SaveThenLoad()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var snapshot = new Dictionary<string, object?> { ["key"] = "value" };
        await store.SaveAsync("wf-1", 2, snapshot);
        var cp = await store.LoadAsync("wf-1");

        await Given("a checkpoint saved at step 2 with a key/value pair", () => cp)
            .Then("the loaded checkpoint has matching index and value", loaded =>
            {
                loaded.Should().NotBeNull();
                loaded!.StepIndex.Should().Be(2);
                loaded.ContextSnapshot["key"].Should().Be("value");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Load for unknown workflow ID returns null"), Fact]
    public async Task LoadMissingReturnsNull()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var cp = await store.LoadAsync("no-such-workflow");

        await Given("an attempt to load a non-existent checkpoint", () => cp)
            .Then("the result is null", result =>
            {
                result.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Clear removes the stored checkpoint"), Fact]
    public async Task ClearRemovesCheckpoint()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        await store.SaveAsync("wf-2", 0, new Dictionary<string, object?>());
        await store.ClearAsync("wf-2");
        var cp = await store.LoadAsync("wf-2");

        await Given("a cleared checkpoint", () => cp)
            .Then("the loaded result is null", result =>
            {
                result.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Saving again overwrites the previous checkpoint"), Fact]
    public async Task OverwriteCheckpoint()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        await store.SaveAsync("wf-3", 1, new Dictionary<string, object?> { ["x"] = 1 });
        await store.SaveAsync("wf-3", 3, new Dictionary<string, object?> { ["x"] = 99 });
        var cp = await store.LoadAsync("wf-3");

        await Given("a checkpoint overwritten at step 3", () => cp)
            .Then("the loaded checkpoint reflects the latest save", loaded =>
            {
                loaded.Should().NotBeNull();
                loaded!.StepIndex.Should().Be(3);
                loaded.ContextSnapshot["x"].Should().Be(99);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Concurrent saves for different workflow IDs are isolated"), Fact]
    public async Task ConcurrentSavesAreIsolated()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var t1 = store.SaveAsync("wf-a", 0, new Dictionary<string, object?> { ["owner"] = "a" });
        var t2 = store.SaveAsync("wf-b", 0, new Dictionary<string, object?> { ["owner"] = "b" });
        await Task.WhenAll(t1, t2);

        var cpA = await store.LoadAsync("wf-a");
        var cpB = await store.LoadAsync("wf-b");

        await Given("two concurrently saved checkpoints for different IDs", () => (cpA, cpB))
            .Then("each has its own owner value", pair =>
            {
                pair.cpA!.ContextSnapshot["owner"].Should().Be("a");
                pair.cpB!.ContextSnapshot["owner"].Should().Be("b");
                return true;
            })
            .AssertPassed();
    }
}
