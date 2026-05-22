using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Agents;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Agents.Tests.Agents;

[Feature("InMemoryCheckpointStore — stores and retrieves agent context snapshots")]
public class InMemoryCheckpointStoreScenarios : TinyBddXunitBase
{
    public InMemoryCheckpointStoreScenarios(ITestOutputHelper output) : base(output) { }

    private static ContextSnapshot MakeSnapshot(string stepName = "step1") =>
        new ContextSnapshot
        {
            StepName = stepName,
            Messages = new List<ConversationMessage>
            {
                new ConversationMessage { Role = ConversationRole.User, Content = "hello" }
            }
        };

    [Scenario("Saved checkpoint can be loaded back"), Fact]
    public async Task Save_ThenLoad_RoundTrips()
    {
        var store = new InMemoryCheckpointStore();
        var snapshot = MakeSnapshot("step1");
        await store.SaveAsync("wf-1", "cp-1", snapshot);

        var loaded = await store.LoadAsync("wf-1", "cp-1");

        await Given("a snapshot saved with workflowId=wf-1 checkpointId=cp-1", () => loaded)
            .Then("the loaded snapshot has the same StepName", s =>
            {
                s.Should().NotBeNull();
                s!.StepName.Should().Be("step1");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Loading non-existent checkpoint returns null"), Fact]
    public async Task Load_NonExistent_ReturnsNull()
    {
        var store = new InMemoryCheckpointStore();
        var result = await store.LoadAsync("no-workflow", "no-checkpoint");

        await Given("a store with no saved checkpoints", () => result)
            .Then("result is null", r =>
            {
                r.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("List returns all checkpoints for a workflow"), Fact]
    public async Task List_ReturnsAllCheckpoints()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync("wf-x", "cp-a", MakeSnapshot("stepA"));
        await store.SaveAsync("wf-x", "cp-b", MakeSnapshot("stepB"));

        var list = await store.ListAsync("wf-x");

        await Given("two checkpoints saved for wf-x", () => list)
            .Then("list contains 2 entries", l =>
            {
                l.Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("List for unknown workflow returns empty"), Fact]
    public async Task List_UnknownWorkflow_ReturnsEmpty()
    {
        var store = new InMemoryCheckpointStore();
        var list = await store.ListAsync("unknown");

        await Given("no checkpoints saved", () => list)
            .Then("empty list returned", l =>
            {
                l.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Saving over existing checkpoint replaces it"), Fact]
    public async Task Save_Overwrites_ExistingCheckpoint()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync("wf-1", "cp-1", MakeSnapshot("original"));
        await store.SaveAsync("wf-1", "cp-1", MakeSnapshot("updated"));

        var loaded = await store.LoadAsync("wf-1", "cp-1");

        await Given("checkpoint cp-1 saved twice with different step names", () => loaded)
            .Then("the second save wins", s =>
            {
                s!.StepName.Should().Be("updated");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("SaveAsync with null workflowId throws"), Fact]
    public async Task Save_NullWorkflowId_Throws()
    {
        var store = new InMemoryCheckpointStore();
        Exception? caught = null;
        try { await store.SaveAsync(null!, "cp", MakeSnapshot()); }
        catch (Exception ex) { caught = ex; }

        await Given("null workflowId", () => caught)
            .Then("ArgumentNullException thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CheckpointInfo is populated after save"), Fact]
    public async Task CheckpointInfo_IsPopulated()
    {
        var store = new InMemoryCheckpointStore();
        var snapshot = MakeSnapshot("s1");
        await store.SaveAsync("wf-2", "cp-2", snapshot);

        var list = await store.ListAsync("wf-2");

        await Given("a saved checkpoint", () => list.First())
            .Then("CheckpointInfo has correct ids and step name", info =>
            {
                info.Id.Should().Be("cp-2");
                info.WorkflowId.Should().Be("wf-2");
                info.StepName.Should().Be("s1");
                return true;
            })
            .AssertPassed();
    }
}
