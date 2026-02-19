using FluentAssertions;
using WorkflowFramework.Extensions.Events;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Events;

public class InMemoryEventBusTests
{
    [Fact]
    public async Task PublishAsync_NullEvent_Throws()
    {
        var bus = new InMemoryEventBus();
        await bus.Invoking(b => b.PublishAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishSubscribe_DeliversEvent()
    {
        var bus = new InMemoryEventBus();
        WorkflowEvent? received = null;
        bus.Subscribe("order", e => { received = e; return Task.CompletedTask; });
        await bus.PublishAsync(new WorkflowEvent { EventType = "order" });
        received.Should().NotBeNull();
    }

    [Fact]
    public async Task Subscribe_MultipleHandlers_AllReceive()
    {
        var bus = new InMemoryEventBus();
        var count = 0;
        bus.Subscribe("e", _ => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        bus.Subscribe("e", _ => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        await bus.PublishAsync(new WorkflowEvent { EventType = "e" });
        count.Should().Be(2);
    }

    [Fact]
    public async Task Unsubscribe_RemovesHandler()
    {
        var bus = new InMemoryEventBus();
        var called = false;
        var sub = bus.Subscribe("e", _ => { called = true; return Task.CompletedTask; });
        sub.Dispose();
        await bus.PublishAsync(new WorkflowEvent { EventType = "e" });
        called.Should().BeFalse();
    }

    [Fact]
    public async Task DeadLetters_WhenNoSubscribers()
    {
        var bus = new InMemoryEventBus();
        await bus.PublishAsync(new WorkflowEvent { EventType = "orphan" });
        bus.DeadLetters.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeadLetters_AfterUnsubscribe()
    {
        var bus = new InMemoryEventBus();
        var sub = bus.Subscribe("e", _ => Task.CompletedTask);
        sub.Dispose();
        await bus.PublishAsync(new WorkflowEvent { EventType = "e" });
        bus.DeadLetters.Should().HaveCount(1);
    }

    [Fact]
    public async Task WaitForEvent_ReceivesCorrelatedEvent()
    {
        var bus = new InMemoryEventBus();
        var waitTask = bus.WaitForEventAsync("cb", "c1", TimeSpan.FromSeconds(5));
        await Task.Delay(30);
        await bus.PublishAsync(new WorkflowEvent { EventType = "cb", CorrelationId = "c1" });
        var result = await waitTask;
        result.Should().NotBeNull();
        result!.CorrelationId.Should().Be("c1");
    }

    [Fact]
    public async Task WaitForEvent_Timeout_ReturnsNull()
    {
        var bus = new InMemoryEventBus();
        var result = await bus.WaitForEventAsync("x", "y", TimeSpan.FromMilliseconds(30));
        result.Should().BeNull();
    }

    [Fact]
    public async Task WaitForEvent_Cancellation_ReturnsNull()
    {
        var bus = new InMemoryEventBus();
        using var cts = new CancellationTokenSource(30);
        var result = await bus.WaitForEventAsync("x", "y", TimeSpan.FromSeconds(30), cts.Token);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Publish_ToCorrelatedWaiter_DoesNotGoToSubscribers()
    {
        var bus = new InMemoryEventBus();
        var subscriberCalled = false;
        bus.Subscribe("cb", _ => { subscriberCalled = true; return Task.CompletedTask; });

        var waitTask = bus.WaitForEventAsync("cb", "c1", TimeSpan.FromSeconds(5));
        await Task.Delay(30);
        await bus.PublishAsync(new WorkflowEvent { EventType = "cb", CorrelationId = "c1" });
        await waitTask;
        subscriberCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentPublish_IsThreadSafe()
    {
        var bus = new InMemoryEventBus();
        var count = 0;
        bus.Subscribe("e", _ => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        var tasks = Enumerable.Range(0, 100).Select(_ =>
            bus.PublishAsync(new WorkflowEvent { EventType = "e" }));
        await Task.WhenAll(tasks);
        count.Should().Be(100);
    }
}
