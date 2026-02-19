using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class InMemoryCheckpointStoreTests
{
    [Fact]
    public async Task SaveAsync_And_LoadAsync_RoundTrips()
    {
        var store = new InMemoryCheckpointStore();
        var snapshot = new ContextSnapshot
        {
            Messages = { new ConversationMessage { Content = "hello" } }
        };

        await store.SaveAsync("wf1", "cp1", snapshot);
        var loaded = await store.LoadAsync("wf1", "cp1");

        loaded.Should().NotBeNull();
        loaded!.Messages.Should().HaveCount(1);
        loaded.Messages[0].Content.Should().Be("hello");
    }

    [Fact]
    public async Task LoadAsync_MissingCheckpoint_ReturnsNull()
    {
        var store = new InMemoryCheckpointStore();
        var result = await store.LoadAsync("wf1", "missing");
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_MissingWorkflow_ReturnsNull()
    {
        var store = new InMemoryCheckpointStore();
        var result = await store.LoadAsync("nonexistent", "cp1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsCheckpointInfos()
    {
        var store = new InMemoryCheckpointStore();
        var s1 = new ContextSnapshot { Messages = { new ConversationMessage { Content = "a" } } };
        var s2 = new ContextSnapshot { Messages = { new ConversationMessage { Content = "b" } } };

        await store.SaveAsync("wf1", "cp1", s1);
        await store.SaveAsync("wf1", "cp2", s2);

        var list = await store.ListAsync("wf1");
        list.Should().HaveCount(2);
        list.Select(i => i.Id).Should().Contain("cp1").And.Contain("cp2");
        list[0].WorkflowId.Should().Be("wf1");
    }

    [Fact]
    public async Task ListAsync_EmptyWorkflow_ReturnsEmpty()
    {
        var store = new InMemoryCheckpointStore();
        var list = await store.ListAsync("nonexistent");
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_RemovesCheckpoint()
    {
        var store = new InMemoryCheckpointStore();
        var snapshot = new ContextSnapshot { Messages = { new ConversationMessage { Content = "x" } } };
        await store.SaveAsync("wf1", "cp1", snapshot);

        await store.DeleteAsync("wf1", "cp1");

        var loaded = await store.LoadAsync("wf1", "cp1");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_MissingCheckpoint_DoesNotThrow()
    {
        var store = new InMemoryCheckpointStore();
        var act = async () => await store.DeleteAsync("wf1", "missing");
        await act.Should().NotThrowAsync();
    }
}
