# Enterprise Integration Patterns

The `Extensions.Integration` package implements classic [Enterprise Integration Patterns](https://www.enterpriseintegrationpatterns.com/) as first-class workflow steps with fluent builder extensions.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Integration
```

## Routing Patterns

### Content-Based Router

Routes messages to different steps based on predicates:

```csharp
using WorkflowFramework.Extensions.Integration.Builder;

var workflow = new WorkflowBuilder()
    .Route(new[]
    {
        ((Func<IWorkflowContext, bool>)(ctx => ctx.Properties["Type"]?.ToString() == "order"), processOrder),
        ((Func<IWorkflowContext, bool>)(ctx => ctx.Properties["Type"]?.ToString() == "invoice"), processInvoice),
    }, defaultRoute: logUnknown)
    .Build();
```

### Message Filter

Drops messages that don't match a predicate:

```csharp
var workflow = new WorkflowBuilder()
    .Filter(ctx => ctx.Properties.ContainsKey("CustomerId"))
    .Step(processStep)
    .Build();
```

### Dynamic Router

Routing logic determined at runtime by a function:

```csharp
var workflow = new WorkflowBuilder()
    .DynamicRoute(ctx =>
    {
        var priority = ctx.Properties["Priority"]?.ToString();
        return priority == "high" ? urgentStep : normalStep;
    })
    .Build();
```

### Recipient List

Sends to multiple recipients selected at runtime:

```csharp
var workflow = new WorkflowBuilder()
    .RecipientList(
        ctx => GetSubscribers(ctx).Select(s => new NotifyStep(s)),
        parallel: true)
    .Build();
```

## Composition Patterns

### Splitter

Breaks a collection into individual items and processes each:

```csharp
var workflow = new WorkflowBuilder()
    .Split(
        ctx => (IEnumerable<object>)ctx.Properties["Orders"]!,
        processOneOrder,
        parallel: true)
    .Build();
```

### Aggregator

Collects items and combines them when a condition is met:

```csharp
var workflow = new WorkflowBuilder()
    .Aggregate(
        ctx => (IEnumerable<object>)ctx.Properties["Items"]!,
        async (items, ctx) =>
        {
            ctx.Properties["BatchResult"] = items.Count;
        },
        opts => opts.CompletionSize = 10)
    .Build();
```

### Scatter-Gather

Sends to multiple handlers in parallel and aggregates results:

```csharp
var workflow = new WorkflowBuilder()
    .ScatterGather(
        new IStep[] { priceServiceA, priceServiceB, priceServiceC },
        async (results, ctx) =>
        {
            ctx.Properties["BestPrice"] = results.Min();
        },
        timeout: TimeSpan.FromSeconds(5))
    .Build();
```

### Resequencer

Reorders out-of-sequence items:

```csharp
var workflow = new WorkflowBuilder()
    .Resequence(
        ctx => (IEnumerable<object>)ctx.Properties["Messages"]!,
        msg => ((dynamic)msg).SequenceNumber)
    .Build();
```

## Transformation Patterns

### Content Enricher

Enriches a message with additional data from an external source:

```csharp
var workflow = new WorkflowBuilder()
    .Enrich(async ctx =>
    {
        var customerId = ctx.Properties["CustomerId"]!.ToString()!;
        var details = await customerService.GetAsync(customerId);
        ctx.Properties["CustomerName"] = details.Name;
    })
    .Build();
```

## Channel Patterns

### Wire Tap

Inspect messages without affecting the main flow (for logging/audit):

```csharp
var workflow = new WorkflowBuilder()
    .WireTap(async ctx =>
    {
        logger.LogInformation("Processing order {Id}", ctx.Properties["OrderId"]);
    })
    .Step(processStep)
    .Build();
```

### Dead Letter

Wraps a step and sends failures to a dead letter store instead of throwing:

```csharp
var workflow = new WorkflowBuilder()
    .WithDeadLetter(deadLetterStore, riskyStep)
    .Build();
```

### Claim Check

Store large payloads externally and replace with a claim ticket:

```csharp
var workflow = new WorkflowBuilder()
    .ClaimCheck(store, ctx => ctx.Properties["LargePayload"]!)
    // ... lightweight processing ...
    .ClaimRetrieve(store)
    .Build();
```

## Endpoint Patterns

### Idempotent Receiver

The `IdempotentReceiverStep` ensures duplicate messages are only processed once.

### Transactional Outbox

The `TransactionalOutboxStep` stores outgoing messages in a transactional outbox before dispatching.

### Polling Consumer

The `PollingConsumerStep` polls an external source at a configurable interval.

> [!TIP]
> All integration pattern steps implement `IStep` and can be composed with any other WorkflowFramework feature — branching, middleware, persistence, etc.
