using FluentAssertions;
using WorkflowFramework.Extensions.Events;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Events;

public class EventBuilderExtensionsTests
{
    [Fact]
    public void PublishEvent_AddsPublishStep()
    {
        var bus = new InMemoryEventBus();
        var workflow = Workflow.Create("test")
            .PublishEvent(bus, "order.created")
            .Build();

        workflow.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void PublishEvent_WithName_SetsName()
    {
        var bus = new InMemoryEventBus();
        var workflow = Workflow.Create("test")
            .PublishEvent(bus, "order.created", "Publish")
            .Build();

        workflow.Steps[0].Name.Should().Be("Publish");
    }

    [Fact]
    public void WaitForEvent_AddsWaitStep()
    {
        var bus = new InMemoryEventBus();
        var workflow = Workflow.Create("test")
            .WaitForEvent(bus, "order.shipped", TimeSpan.FromSeconds(5))
            .Build();

        workflow.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void WaitForEvent_WithName_SetsName()
    {
        var bus = new InMemoryEventBus();
        var workflow = Workflow.Create("test")
            .WaitForEvent(bus, "order.shipped", TimeSpan.FromSeconds(5), "Wait")
            .Build();

        workflow.Steps[0].Name.Should().Be("Wait");
    }

    [Fact]
    public async Task PublishEvent_ExecutesAndPublishes()
    {
        var bus = new InMemoryEventBus();
        WorkflowEvent? received = null;
        bus.Subscribe("test.event", e => { received = e; return Task.CompletedTask; });

        var workflow = Workflow.Create("test")
            .PublishEvent(bus, "test.event")
            .Build();

        var ctx = new WorkflowContext();
        await workflow.ExecuteAsync(ctx);

        received.Should().NotBeNull();
        received!.EventType.Should().Be("test.event");
    }
}
