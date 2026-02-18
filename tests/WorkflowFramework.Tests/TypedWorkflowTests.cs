using FluentAssertions;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests;

public class TypedWorkflowTests
{
    public class OrderData
    {
        public string OrderId { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public decimal Total { get; set; }
    }

    private class ValidateOrderStep : IStep<OrderData>
    {
        public string Name => "ValidateOrder";

        public Task ExecuteAsync(IWorkflowContext<OrderData> context)
        {
            context.Data.IsValid = context.Data.Total > 0;
            TrackingStep.GetLog(context).Add(Name);
            return Task.CompletedTask;
        }
    }

    private class ProcessOrderStep : IStep<OrderData>
    {
        public string Name => "ProcessOrder";

        public Task ExecuteAsync(IWorkflowContext<OrderData> context)
        {
            TrackingStep.GetLog(context).Add(Name);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Given_TypedWorkflow_When_Executed_Then_DataFlowsThrough()
    {
        // Given
        var workflow = Workflow.Create<OrderData>("OrderWorkflow")
            .Step(new ValidateOrderStep())
            .Step(new ProcessOrderStep())
            .Build();

        var data = new OrderData { OrderId = "ORD-001", Total = 99.99m };
        var context = new WorkflowContext<OrderData>(data);

        // When
        var result = await workflow.ExecuteAsync(context);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Data.IsValid.Should().BeTrue();
        TrackingStep.GetLog(context).Should().ContainInOrder("ValidateOrder", "ProcessOrder");
    }

    [Fact]
    public async Task Given_TypedConditional_When_ConditionTrue_Then_ThenBranchRuns()
    {
        // Given
        var workflow = Workflow.Create<OrderData>()
            .Step(new ValidateOrderStep())
            .If(ctx => ctx.Data.IsValid)
                .Then(new ProcessOrderStep())
                .EndIf()
            .Build();

        var data = new OrderData { Total = 50m };
        var context = new WorkflowContext<OrderData>(data);

        // When
        var result = await workflow.ExecuteAsync(context);

        // Then
        result.IsSuccess.Should().BeTrue();
        TrackingStep.GetLog(context).Should().ContainInOrder("ValidateOrder", "ProcessOrder");
    }
}
