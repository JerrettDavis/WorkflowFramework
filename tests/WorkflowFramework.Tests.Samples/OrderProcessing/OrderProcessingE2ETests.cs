using FluentAssertions;
using WorkflowFramework;
using WorkflowFramework.Extensions.Visualization;
using Xunit;

namespace WorkflowFramework.Tests.Samples.OrderProcessing;

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
        return Task.CompletedTask;
    }
}

public class CheckInventoryStep : ICompensatingStep<OrderData>
{
    public string Name => "CheckInventory";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.InventoryReserved = true;
        return Task.CompletedTask;
    }
    public Task CompensateAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.InventoryReserved = false;
        return Task.CompletedTask;
    }
}

public class PrioritizeOrderStep : IStep<OrderData>
{
    public string Name => "PrioritizeOrder";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.IsPrioritized = true;
        return Task.CompletedTask;
    }
}

public class StandardProcessingStep : IStep<OrderData>
{
    public string Name => "StandardProcessing";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        return Task.CompletedTask;
    }
}

public class ChargePaymentStep : ICompensatingStep<OrderData>
{
    public string Name => "ChargePayment";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.PaymentConfirmed = true;
        return Task.CompletedTask;
    }
    public Task CompensateAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.PaymentConfirmed = false;
        return Task.CompletedTask;
    }
}

public class SendConfirmationStep : IStep<OrderData>
{
    public string Name => "SendConfirmation";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.ConfirmationSent = true;
        return Task.CompletedTask;
    }
}

public class FailingStep : IStep<OrderData>
{
    public string Name => "FailingStep";
    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        throw new InvalidOperationException("Simulated failure for compensation test.");
    }
}

public class OrderProcessingE2ETests
{
    private static IWorkflow<OrderData> BuildOrderWorkflow()
    {
        return Workflow.Create<OrderData>("OrderProcessing")
            .WithCompensation()
            .Step<ValidateOrderStep>()
            .Step<CheckInventoryStep>()
            .If(ctx => ctx.Data.IsExpressShipping)
                .Then<PrioritizeOrderStep>()
                .Else<StandardProcessingStep>()
            .Step<ChargePaymentStep>()
            .Step<SendConfirmationStep>()
            .Build();
    }

    [Fact]
    public async Task OrderWorkflow_ExpressShipping_PrioritizesAndCompletes()
    {
        var workflow = BuildOrderWorkflow();
        var data = new OrderData
        {
            OrderId = "ORD-1",
            CustomerId = "CUST-1",
            Items = ["Widget"],
            TotalAmount = 50m,
            IsExpressShipping = true
        };

        var result = await workflow.ExecuteAsync(new WorkflowContext<OrderData>(data));

        result.Status.Should().Be(WorkflowStatus.Completed);
        result.Data.PaymentConfirmed.Should().BeTrue();
        result.Data.ConfirmationSent.Should().BeTrue();
        result.Data.IsPrioritized.Should().BeTrue();
    }

    [Fact]
    public async Task OrderWorkflow_StandardShipping_ProcessesAndCompletes()
    {
        var workflow = BuildOrderWorkflow();
        var data = new OrderData
        {
            OrderId = "ORD-2",
            CustomerId = "CUST-2",
            Items = ["Gadget"],
            TotalAmount = 30m,
            IsExpressShipping = false
        };

        var result = await workflow.ExecuteAsync(new WorkflowContext<OrderData>(data));

        result.Status.Should().Be(WorkflowStatus.Completed);
        result.Data.PaymentConfirmed.Should().BeTrue();
        result.Data.ConfirmationSent.Should().BeTrue();
        result.Data.IsPrioritized.Should().BeFalse();
    }

    [Fact]
    public void OrderWorkflow_Visualization_GeneratesMermaid()
    {
        var engine = Workflow.Create("OrderProcessing")
            .Step("ValidateOrder", _ => Task.CompletedTask)
            .Step("CheckInventory", _ => Task.CompletedTask)
            .Step("ChargePayment", _ => Task.CompletedTask)
            .Step("SendConfirmation", _ => Task.CompletedTask)
            .Build();

        var mermaid = engine.ToMermaid();

        mermaid.Should().Contain("graph TD");
        mermaid.Should().Contain("ValidateOrder");
        mermaid.Should().Contain("CheckInventory");
        mermaid.Should().Contain("ChargePayment");
        mermaid.Should().Contain("SendConfirmation");
    }

    [Fact]
    public async Task OrderWorkflow_Compensation_RollsBackOnFailure()
    {
        var workflow = Workflow.Create<OrderData>("OrderProcessing")
            .WithCompensation()
            .Step<ValidateOrderStep>()
            .Step<CheckInventoryStep>()
            .Step<ChargePaymentStep>()
            .Step<FailingStep>()
            .Build();

        var data = new OrderData
        {
            OrderId = "ORD-FAIL",
            CustomerId = "CUST-FAIL",
            Items = ["Widget"],
            TotalAmount = 99m
        };

        var result = await workflow.ExecuteAsync(new WorkflowContext<OrderData>(data));

        // With compensation enabled, the workflow should be compensated and roll back
        result.Status.Should().Be(WorkflowStatus.Compensated);
        // Compensating steps should have undone their work
        result.Data.InventoryReserved.Should().BeFalse("CheckInventoryStep compensation should release inventory");
        result.Data.PaymentConfirmed.Should().BeFalse("ChargePaymentStep compensation should refund payment");
    }
}
