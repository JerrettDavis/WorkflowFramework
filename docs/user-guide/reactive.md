# Reactive Extensions

The `Extensions.Reactive` package adds streaming step support via `IAsyncEnumerable<T>`.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Reactive
```

## IAsyncStep&lt;T&gt;

A step that produces a stream of results instead of a single output:

```csharp
using WorkflowFramework.Extensions.Reactive;

public class PagedFetchStep : IAsyncStep<Order>
{
    public string Name => "FetchOrders";

    public async IAsyncEnumerable<Order> ExecuteStreamingAsync(IWorkflowContext context)
    {
        int page = 0;
        while (true)
        {
            var batch = await api.GetOrdersAsync(page++);
            if (batch.Count == 0) yield break;
            foreach (var order in batch)
                yield return order;
        }
    }
}
```

## AsyncStepAdapter&lt;T&gt;

Wraps an `IAsyncStep<T>` so it can be used as a standard `IStep` in any workflow. Results are collected into a list and stored in context:

```csharp
var step = new AsyncStepAdapter<Order>(new PagedFetchStep());

var workflow = new WorkflowBuilder()
    .Step(step)
    .Build();

await workflow.RunAsync(context);
var orders = (List<Order>)context.Properties["FetchOrders.Results"];
```

## Extension Methods

```csharp
// Collect all results
List<Order> orders = await asyncStep.CollectAsync(context);

// Process each result as it arrives
await asyncStep.ForEachAsync(context, async order =>
{
    await ProcessOrderAsync(order);
});
```
