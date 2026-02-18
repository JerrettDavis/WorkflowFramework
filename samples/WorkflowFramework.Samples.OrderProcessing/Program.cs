using WorkflowFramework;
using WorkflowFramework.Extensions.Visualization;

// === Order Processing Workflow with Saga/Compensation ===

var workflow = Workflow.Create<OrderData>("OrderProcessing")
    .WithCompensation()
    .Step<ValidateOrderStep>()
    .Step<CheckInventoryStep>()
    .If(ctx => ctx.Data.IsExpressShipping)
        .Then<PrioritizeOrderStep>()
        .Else<StandardProcessingStep>()
    .Step<ChargePaymentStep>()
    .Step<SendConfirmationStep>()
    .Build();

// Visualize the workflow
var engine = Workflow.Create("OrderProcessing")
    .Step("ValidateOrder", _ => Task.CompletedTask)
    .Step("CheckInventory", _ => Task.CompletedTask)
    .Step("ChargePayment", _ => Task.CompletedTask)
    .Step("SendConfirmation", _ => Task.CompletedTask)
    .Build();

Console.WriteLine("=== Order Processing Workflow ===");
Console.WriteLine();
Console.WriteLine("Mermaid Diagram:");
Console.WriteLine(engine.ToMermaid());

// Execute the workflow
var data = new OrderData
{
    OrderId = "ORD-12345",
    CustomerId = "CUST-001",
    Items = ["Widget A", "Gadget B"],
    TotalAmount = 99.99m,
    IsExpressShipping = true
};

var context = new WorkflowContext<OrderData>(data);
var result = await workflow.ExecuteAsync(context);

Console.WriteLine($"Workflow Status: {result.Status}");
Console.WriteLine($"Order ID: {result.Data.OrderId}");
Console.WriteLine($"Payment Confirmed: {result.Data.PaymentConfirmed}");
Console.WriteLine($"Confirmation Sent: {result.Data.ConfirmationSent}");

// === Domain Types ===

public class OrderData
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public bool IsExpressShipping { get; set; }
    public bool InventoryReserved { get; set; }
    public bool PaymentConfirmed { get; set; }
    public bool ConfirmationSent { get; set; }
    public bool IsPrioritized { get; set; }
}

// === Steps ===

public class ValidateOrderStep : IStep<OrderData>
{
    public string Name => "ValidateOrder";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        var data = context.Data;
        if (string.IsNullOrEmpty(data.OrderId))
            throw new InvalidOperationException("Order ID is required.");
        if (data.Items.Count == 0)
            throw new InvalidOperationException("Order must have at least one item.");
        Console.WriteLine($"  ✓ Order {data.OrderId} validated");
        return Task.CompletedTask;
    }
}

public class CheckInventoryStep : ICompensatingStep<OrderData>
{
    public string Name => "CheckInventory";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.InventoryReserved = true;
        Console.WriteLine($"  ✓ Inventory reserved for {context.Data.Items.Count} items");
        return Task.CompletedTask;
    }
    public Task CompensateAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.InventoryReserved = false;
        Console.WriteLine("  ↩ Inventory reservation released");
        return Task.CompletedTask;
    }
}

public class PrioritizeOrderStep : IStep<OrderData>
{
    public string Name => "PrioritizeOrder";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.IsPrioritized = true;
        Console.WriteLine("  ✓ Order prioritized for express shipping");
        return Task.CompletedTask;
    }
}

public class StandardProcessingStep : IStep<OrderData>
{
    public string Name => "StandardProcessing";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        Console.WriteLine("  ✓ Standard processing applied");
        return Task.CompletedTask;
    }
}

public class ChargePaymentStep : ICompensatingStep<OrderData>
{
    public string Name => "ChargePayment";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.PaymentConfirmed = true;
        Console.WriteLine($"  ✓ Payment of ${context.Data.TotalAmount} charged");
        return Task.CompletedTask;
    }
    public Task CompensateAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.PaymentConfirmed = false;
        Console.WriteLine($"  ↩ Payment of ${context.Data.TotalAmount} refunded");
        return Task.CompletedTask;
    }
}

public class SendConfirmationStep : IStep<OrderData>
{
    public string Name => "SendConfirmation";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.ConfirmationSent = true;
        Console.WriteLine($"  ✓ Confirmation email sent for order {context.Data.OrderId}");
        return Task.CompletedTask;
    }
}
