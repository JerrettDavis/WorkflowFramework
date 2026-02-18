using WorkflowFramework;
using WorkflowFramework.Extensions.Diagnostics;

// ── Simple untyped workflow ──────────────────────────────────────

var simple = Workflow.Create("HelloWorkflow")
    .Use(new TimingMiddleware())
    .Step("Greet", ctx =>
    {
        Console.WriteLine($"[{ctx.WorkflowId}] Hello from step!");
        return Task.CompletedTask;
    })
    .Step("Farewell", ctx =>
    {
        Console.WriteLine($"[{ctx.WorkflowId}] Goodbye!");
        return Task.CompletedTask;
    })
    .Build();

var result = await simple.ExecuteAsync(new WorkflowContext());
Console.WriteLine($"Status: {result.Status}");
Console.WriteLine();

// ── Typed workflow with branching ────────────────────────────────

var orderWorkflow = Workflow.Create<OrderData>("OrderPipeline")
    .Step(new ValidateOrder())
    .If(ctx => ctx.Data.IsValid)
        .Then(new ProcessOrder())
        .Else(new RejectOrder())
    .Step("Summary", ctx =>
    {
        Console.WriteLine($"Order {ctx.Data.OrderId}: Valid={ctx.Data.IsValid}, Processed={ctx.Data.IsProcessed}");
        return Task.CompletedTask;
    })
    .Build();

var order = new OrderData { OrderId = "ORD-42", Total = 99.99m };
var orderResult = await orderWorkflow.ExecuteAsync(new WorkflowContext<OrderData>(order));
Console.WriteLine($"Order workflow: {orderResult.Status}");

// ── Types ────────────────────────────────────────────────────────

public class OrderData
{
    public string OrderId { get; set; } = "";
    public decimal Total { get; set; }
    public bool IsValid { get; set; }
    public bool IsProcessed { get; set; }
}

public class ValidateOrder : IStep<OrderData>
{
    public string Name => "ValidateOrder";
    public Task ExecuteAsync(IWorkflowContext<OrderData> ctx)
    {
        ctx.Data.IsValid = ctx.Data.Total > 0;
        return Task.CompletedTask;
    }
}

public class ProcessOrder : IStep<OrderData>
{
    public string Name => "ProcessOrder";
    public Task ExecuteAsync(IWorkflowContext<OrderData> ctx)
    {
        ctx.Data.IsProcessed = true;
        Console.WriteLine($"  Processing order {ctx.Data.OrderId}...");
        return Task.CompletedTask;
    }
}

public class RejectOrder : IStep<OrderData>
{
    public string Name => "RejectOrder";
    public Task ExecuteAsync(IWorkflowContext<OrderData> ctx)
    {
        Console.WriteLine($"  Rejecting order {ctx.Data.OrderId}!");
        return Task.CompletedTask;
    }
}
