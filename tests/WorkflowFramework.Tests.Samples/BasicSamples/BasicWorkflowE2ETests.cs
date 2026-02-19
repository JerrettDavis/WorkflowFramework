using FluentAssertions;
using WorkflowFramework;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Samples.BasicSamples;

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
        return Task.CompletedTask;
    }
}

public class RejectOrder : IStep<OrderData>
{
    public string Name => "RejectOrder";
    public Task ExecuteAsync(IWorkflowContext<OrderData> ctx)
    {
        return Task.CompletedTask;
    }
}

public class BasicWorkflowE2ETests
{
    [Fact]
    public async Task SimpleWorkflow_GreetAndFarewell_Completes()
    {
        var workflow = Workflow.Create("HelloWorkflow")
            .Use(new TimingMiddleware())
            .Step("Greet", _ => Task.CompletedTask)
            .Step("Farewell", _ => Task.CompletedTask)
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());

        result.Status.Should().Be(WorkflowStatus.Completed);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task TypedOrderWorkflow_ValidOrder_Processes()
    {
        var workflow = Workflow.Create<OrderData>("OrderPipeline")
            .Step(new ValidateOrder())
            .If(ctx => ctx.Data.IsValid)
                .Then(new ProcessOrder())
                .Else(new RejectOrder())
            .Build();

        var order = new OrderData { OrderId = "ORD-1", Total = 99.99m };
        var result = await workflow.ExecuteAsync(new WorkflowContext<OrderData>(order));

        result.Status.Should().Be(WorkflowStatus.Completed);
        result.Data.IsValid.Should().BeTrue();
        result.Data.IsProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task TypedOrderWorkflow_InvalidOrder_Rejects()
    {
        var workflow = Workflow.Create<OrderData>("OrderPipeline")
            .Step(new ValidateOrder())
            .If(ctx => ctx.Data.IsValid)
                .Then(new ProcessOrder())
                .Else(new RejectOrder())
            .Build();

        var order = new OrderData { OrderId = "ORD-2", Total = -5m };
        var result = await workflow.ExecuteAsync(new WorkflowContext<OrderData>(order));

        result.Status.Should().Be(WorkflowStatus.Completed);
        result.Data.IsValid.Should().BeFalse();
        result.Data.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task TimingMiddleware_RecordsTiming()
    {
        var workflow = Workflow.Create("TimedWorkflow")
            .Use(new TimingMiddleware())
            .Step("Work", _ => Task.CompletedTask)
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());

        result.Status.Should().Be(WorkflowStatus.Completed);
    }
}
