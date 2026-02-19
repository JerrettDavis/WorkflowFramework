using FluentAssertions;
using WorkflowFramework.Extensions.Distributed;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Distributed;

public class WorkflowQueueTests
{
    private readonly InMemoryWorkflowQueue _queue = new();

    [Fact]
    public async Task EnqueueAsync_NullItem_Throws()
    {
        await _queue.Invoking(q => q.EnqueueAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnqueueDequeue_RoundTrip()
    {
        var item = new WorkflowQueueItem { WorkflowName = "Test" };
        await _queue.EnqueueAsync(item);
        var dequeued = await _queue.DequeueAsync();
        dequeued.Should().BeSameAs(item);
    }

    [Fact]
    public async Task DequeueAsync_Empty_ReturnsNull()
    {
        var result = await _queue.DequeueAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLengthAsync_ReflectsCount()
    {
        (await _queue.GetLengthAsync()).Should().Be(0);
        await _queue.EnqueueAsync(new WorkflowQueueItem());
        (await _queue.GetLengthAsync()).Should().Be(1);
        await _queue.DequeueAsync();
        (await _queue.GetLengthAsync()).Should().Be(0);
    }

    [Fact]
    public async Task FIFO_Order()
    {
        await _queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "A" });
        await _queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "B" });
        (await _queue.DequeueAsync())!.WorkflowName.Should().Be("A");
        (await _queue.DequeueAsync())!.WorkflowName.Should().Be("B");
    }

    [Fact]
    public async Task ConcurrentEnqueue_IsThreadSafe()
    {
        var tasks = Enumerable.Range(0, 100).Select(i =>
            _queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = $"W{i}" }));
        await Task.WhenAll(tasks);
        (await _queue.GetLengthAsync()).Should().Be(100);
    }

    [Fact]
    public void WorkflowQueueItem_Defaults()
    {
        var i = new WorkflowQueueItem();
        i.Id.Should().NotBeNullOrEmpty();
        i.WorkflowName.Should().BeEmpty();
        i.SerializedData.Should().BeNull();
        i.EnqueuedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
