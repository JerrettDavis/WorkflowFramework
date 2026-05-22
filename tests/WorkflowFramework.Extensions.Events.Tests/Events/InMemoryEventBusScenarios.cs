using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Events;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Events.Tests.Events;

[Feature("InMemoryEventBus — in-memory event pub/sub bus")]
public class InMemoryEventBusScenarios : TinyBddXunitBase
{
    public InMemoryEventBusScenarios(ITestOutputHelper output) : base(output) { }

    private static WorkflowEvent MakeEvent(string type, string correlationId = "corr-1") =>
        new WorkflowEvent { EventType = type, CorrelationId = correlationId };

    [Scenario("Published event with no subscribers goes to DeadLetters"), Fact]
    public async Task Publish_NoSubscribers_GoesToDeadLetters()
    {
        var bus = new InMemoryEventBus();
        var evt = MakeEvent("no-sub");

        await bus.PublishAsync(evt);

        await Given("event published with no subscribers", () => bus.DeadLetters)
            .Then("event ends up in DeadLetters", dl =>
            {
                dl.Should().Contain(evt);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Published event is delivered to subscriber"), Fact]
    public async Task Publish_WithSubscriber_DeliversEvent()
    {
        var bus = new InMemoryEventBus();
        WorkflowEvent? received = null;
        bus.Subscribe("order.placed", evt => { received = evt; return Task.CompletedTask; });

        var toPublish = MakeEvent("order.placed");
        await bus.PublishAsync(toPublish);

        await Given("event published with a subscriber", () => received)
            .Then("subscriber received the event", r =>
            {
                r.Should().NotBeNull();
                r!.EventType.Should().Be("order.placed");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Published event does NOT go to DeadLetters when subscriber exists"), Fact]
    public async Task Publish_WithSubscriber_NotInDeadLetters()
    {
        var bus = new InMemoryEventBus();
        bus.Subscribe("live-type", _ => Task.CompletedTask);
        await bus.PublishAsync(MakeEvent("live-type"));

        await Given("subscriber exists for event type", () => bus.DeadLetters)
            .Then("DeadLetters is empty", dl =>
            {
                dl.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Subscribe returns disposable that unsubscribes"), Fact]
    public async Task Subscribe_Disposable_Unsubscribes()
    {
        var bus = new InMemoryEventBus();
        var count = 0;
        using (var sub = bus.Subscribe("tick", _ => { count++; return Task.CompletedTask; }))
        {
            await bus.PublishAsync(MakeEvent("tick"));
        }
        // After dispose
        await bus.PublishAsync(MakeEvent("tick"));

        await Given("subscription disposed, then second event published", () => count)
            .Then("handler was called only once (before unsubscribe)", c =>
            {
                c.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WaitForEventAsync resolves when matching event published"), Fact]
    public async Task WaitForEventAsync_ResolvesOnMatch()
    {
        var bus = new InMemoryEventBus();
        var waitTask = bus.WaitForEventAsync("payment.done", "order-42", TimeSpan.FromSeconds(5));

        _ = Task.Run(async () =>
        {
            await Task.Delay(30);
            await bus.PublishAsync(new WorkflowEvent { EventType = "payment.done", CorrelationId = "order-42" });
        });

        var result = await waitTask;

        await Given("WaitForEventAsync called, event published concurrently", () => result)
            .Then("result is not null and has correct type/correlationId", r =>
            {
                r.Should().NotBeNull();
                r!.EventType.Should().Be("payment.done");
                r.CorrelationId.Should().Be("order-42");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WaitForEventAsync returns null on timeout"), Fact]
    public async Task WaitForEventAsync_ReturnsNullOnTimeout()
    {
        var bus = new InMemoryEventBus();
        var result = await bus.WaitForEventAsync("never.happens", "x", TimeSpan.FromMilliseconds(30));

        await Given("no event published within timeout", () => result)
            .Then("result is null", r =>
            {
                r.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("PublishAsync with null event throws ArgumentNullException"), Fact]
    public async Task Publish_NullEvent_Throws()
    {
        var bus = new InMemoryEventBus();
        Exception? caught = null;
        try { await bus.PublishAsync(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("null event published", () => caught)
            .Then("ArgumentNullException thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple subscribers all receive the event"), Fact]
    public async Task MultipleSubscribers_AllReceive()
    {
        var bus = new InMemoryEventBus();
        var count = 0;
        bus.Subscribe("shared", _ => { count++; return Task.CompletedTask; });
        bus.Subscribe("shared", _ => { count++; return Task.CompletedTask; });
        await bus.PublishAsync(MakeEvent("shared"));

        await Given("two subscribers on same event type", () => count)
            .Then("both handlers were called", c =>
            {
                c.Should().Be(2);
                return true;
            })
            .AssertPassed();
    }
}
