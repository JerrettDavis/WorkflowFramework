using FluentAssertions;
using WorkflowFramework.Extensions.Persistence;
using WorkflowFramework.Extensions.Persistence.InMemory;
using Xunit;

namespace WorkflowFramework.Tests.Persistence;

public class CheckpointMiddlewareTests
{
    [Fact]
    public void Constructor_NullStore_Throws()
    {
        var act = () => new CheckpointMiddleware(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_SavesCheckpointAfterStep()
    {
        var store = new InMemoryWorkflowStateStore();
        var middleware = new CheckpointMiddleware(store);
        var context = new WorkflowContext();
        var step = new TestStep("s1");

        await middleware.InvokeAsync(context, step, ctx => Task.CompletedTask);

        var saved = await store.LoadCheckpointAsync(context.WorkflowId);
        saved.Should().NotBeNull();
        saved!.WorkflowId.Should().Be(context.WorkflowId);
        saved.Status.Should().Be(WorkflowStatus.Running);
    }

    private sealed class TestStep : IStep
    {
        public TestStep(string name) => Name = name;
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}
