# Event-Driven Workflows

The `Extensions.Events` package enables workflows that publish and wait for events, supporting choreography-based coordination.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Events
```

## IEventBus

```csharp
using WorkflowFramework.Extensions.Events;

public interface IEventBus
{
    Task PublishAsync(WorkflowEvent evt, CancellationToken ct = default);
    IDisposable Subscribe(string eventType, Func<WorkflowEvent, Task> handler);
    Task<WorkflowEvent?> WaitForEventAsync(string eventType, string correlationId, TimeSpan timeout, CancellationToken ct = default);
}
```

## InMemoryEventBus

A built-in in-memory implementation with dead letter support:

```csharp
var eventBus = new InMemoryEventBus();

// Subscribe
using var sub = eventBus.Subscribe("OrderCompleted", async evt =>
{
    Console.WriteLine($"Order {evt.CorrelationId} completed!");
});

// Publish
await eventBus.PublishAsync(new WorkflowEvent
{
    EventType = "OrderCompleted",
    CorrelationId = "order-123",
    Payload = { ["Total"] = 99.95 }
});

// Undelivered events go to dead letters
var deadLetters = eventBus.DeadLetters;
```

## PublishEventStep

Publishes an event from within a workflow:

```csharp
var workflow = new WorkflowBuilder()
    .PublishEvent(eventBus, "OrderCreated")
    .Build();

await workflow.RunAsync(context);
var eventId = context.Properties["PublishEvent.EventId"];
```

## WaitForEventStep

Pauses the workflow until a matching event arrives (or timeout):

```csharp
var workflow = new WorkflowBuilder()
    .PublishEvent(eventBus, "PaymentRequested")
    .WaitForEvent(eventBus, "PaymentCompleted", timeout: TimeSpan.FromMinutes(30))
    .Build();
```

After the event is received, its payload properties are merged into the context as `WaitFor(PaymentCompleted).{key}`.

## Builder Extensions

```csharp
using WorkflowFramework.Extensions.Events;

// Shorthand via fluent builder
builder
    .PublishEvent(eventBus, "StepCompleted", name: "NotifyDone")
    .WaitForEvent(eventBus, "Approval", TimeSpan.FromHours(1), name: "WaitApproval");
```

> [!NOTE]
> The `InMemoryEventBus` is suitable for single-process scenarios. For distributed systems, implement `IEventBus` over a message broker like RabbitMQ or Azure Service Bus.
