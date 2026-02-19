using FluentAssertions;
using WorkflowFramework.Extensions.Events;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Events;

public class EventStepTests
{
    private TestWorkflowContext CreateContext() => new();

    [Fact]
    public void PublishEventStep_NullBus_Throws()
    {
        FluentActions.Invoking(() => new PublishEventStep(null!, _ => new WorkflowEvent()))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PublishEventStep_NullFactory_Throws()
    {
        FluentActions.Invoking(() => new PublishEventStep(new InMemoryEventBus(), null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PublishEventStep_DefaultName()
    {
        var step = new PublishEventStep(new InMemoryEventBus(), _ => new WorkflowEvent());
        step.Name.Should().Be("PublishEvent");
    }

    [Fact]
    public void PublishEventStep_CustomName()
    {
        var step = new PublishEventStep(new InMemoryEventBus(), _ => new WorkflowEvent(), "Custom");
        step.Name.Should().Be("Custom");
    }

    [Fact]
    public async Task PublishEventStep_PublishesAndSetsProperty()
    {
        var bus = new InMemoryEventBus();
        WorkflowEvent? received = null;
        bus.Subscribe("test", e => { received = e; return Task.CompletedTask; });
        var step = new PublishEventStep(bus, ctx => new WorkflowEvent { EventType = "test", CorrelationId = ctx.CorrelationId });
        var ctx = CreateContext();
        await step.ExecuteAsync(ctx);
        received.Should().NotBeNull();
        ctx.Properties.Should().ContainKey("PublishEvent.EventId");
    }

    [Fact]
    public void WaitForEventStep_NullBus_Throws()
    {
        FluentActions.Invoking(() => new WaitForEventStep(null!, "e", _ => "c", TimeSpan.FromSeconds(1)))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WaitForEventStep_NullCorrelationFactory_Throws()
    {
        FluentActions.Invoking(() => new WaitForEventStep(new InMemoryEventBus(), "e", null!, TimeSpan.FromSeconds(1)))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WaitForEventStep_DefaultName()
    {
        var step = new WaitForEventStep(new InMemoryEventBus(), "myType", _ => "c", TimeSpan.FromSeconds(1));
        step.Name.Should().Be("WaitFor(myType)");
    }

    [Fact]
    public async Task WaitForEventStep_ReceivesEvent_SetsProperties()
    {
        var bus = new InMemoryEventBus();
        var step = new WaitForEventStep(bus, "cb", ctx => "corr1", TimeSpan.FromSeconds(5));
        var ctx = CreateContext();
        var execTask = step.ExecuteAsync(ctx);
        await Task.Delay(30);
        await bus.PublishAsync(new WorkflowEvent
        {
            EventType = "cb",
            CorrelationId = "corr1",
            Payload = new Dictionary<string, object?> { ["key"] = "val" }
        });
        await execTask;
        ctx.Properties["WaitFor(cb).Received"].Should().Be(true);
        ctx.Properties["WaitFor(cb).key"].Should().Be("val");
    }

    [Fact]
    public async Task WaitForEventStep_Timeout_SetsReceivedFalse()
    {
        var bus = new InMemoryEventBus();
        var step = new WaitForEventStep(bus, "cb", _ => "c", TimeSpan.FromMilliseconds(30));
        var ctx = CreateContext();
        await step.ExecuteAsync(ctx);
        ctx.Properties["WaitFor(cb).Received"].Should().Be(false);
    }

    // Shared context for these tests
    private class TestWorkflowContext : IWorkflowContext
    {
        public string WorkflowId { get; set; } = Guid.NewGuid().ToString("N");
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; }
        public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; }
        public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}

public class WorkflowEventTests
{
    [Fact]
    public void Defaults_AreSet()
    {
        var e = new WorkflowEvent();
        e.Id.Should().NotBeNullOrEmpty();
        e.EventType.Should().BeEmpty();
        e.CorrelationId.Should().BeEmpty();
        e.Payload.Should().BeEmpty();
        e.Source.Should().BeNull();
        e.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var e = new WorkflowEvent
        {
            Id = "abc",
            EventType = "order",
            CorrelationId = "c1",
            Source = "system",
            Payload = new Dictionary<string, object?> { ["x"] = 1 }
        };
        e.Id.Should().Be("abc");
        e.Source.Should().Be("system");
    }
}
