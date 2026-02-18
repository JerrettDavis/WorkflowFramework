using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Diagnostics;
using WorkflowFramework.Extensions.Persistence;
using WorkflowFramework.Extensions.Persistence.InMemory;
using WorkflowFramework.Tests.Common;
using Xunit;

namespace WorkflowFramework.Tests.Integration;

public class EndToEndWorkflowTests
{
    public class OrderData
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public bool IsValidated { get; set; }
        public bool IsProcessed { get; set; }
        public bool IsSaved { get; set; }
    }

    private class ValidateStep : IStep<OrderData>
    {
        public string Name => "Validate";
        public Task ExecuteAsync(IWorkflowContext<OrderData> context)
        {
            context.Data.IsValidated = context.Data.Total > 0;
            return Task.CompletedTask;
        }
    }

    private class ProcessStep : IStep<OrderData>
    {
        public string Name => "Process";
        public Task ExecuteAsync(IWorkflowContext<OrderData> context)
        {
            context.Data.IsProcessed = true;
            return Task.CompletedTask;
        }
    }

    private class SaveStep : IStep<OrderData>
    {
        public string Name => "Save";
        public Task ExecuteAsync(IWorkflowContext<OrderData> context)
        {
            context.Data.IsSaved = true;
            return Task.CompletedTask;
        }
    }

    private class RejectStep : IStep<OrderData>
    {
        public string Name => "Reject";
        public Task ExecuteAsync(IWorkflowContext<OrderData> context)
        {
            context.Data.IsProcessed = false;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Given_FullPipeline_When_ValidOrder_Then_ProcessedAndSaved()
    {
        // Given
        var store = new InMemoryWorkflowStateStore();
        var workflow = Workflow.Create<OrderData>("OrderPipeline")
            .Use(new TimingMiddleware())
            .Use(new CheckpointMiddleware(store))
            .Step(new ValidateStep())
            .If(ctx => ctx.Data.IsValidated)
                .Then(new ProcessStep())
                .Else(new RejectStep())
            .Step(new SaveStep())
            .Build();

        var data = new OrderData { OrderId = "ORD-001", Total = 150m };
        var context = new WorkflowContext<OrderData>(data);

        // When
        var result = await workflow.ExecuteAsync(context);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Data.IsValidated.Should().BeTrue();
        result.Data.IsProcessed.Should().BeTrue();
        result.Data.IsSaved.Should().BeTrue();

        // Timings should be recorded
        context.Properties.Should().ContainKey(TimingMiddleware.TimingsKey);

        // Checkpoint should exist
        var state = await store.LoadCheckpointAsync(context.WorkflowId);
        state.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_FullPipeline_When_InvalidOrder_Then_Rejected()
    {
        // Given
        var workflow = Workflow.Create<OrderData>("OrderPipeline")
            .Step(new ValidateStep())
            .If(ctx => ctx.Data.IsValidated)
                .Then(new ProcessStep())
                .Else(new RejectStep())
            .Build();

        var data = new OrderData { OrderId = "ORD-002", Total = -5m };
        var context = new WorkflowContext<OrderData>(data);

        // When
        var result = await workflow.ExecuteAsync(context);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Data.IsValidated.Should().BeFalse();
        result.Data.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_CancellationRequested_When_Running_Then_Aborted()
    {
        // Given
        var cts = new CancellationTokenSource();
        var workflow = Workflow.Create()
            .Step("CancelStep", ctx => { cts.Cancel(); return Task.CompletedTask; })
            .Step(new TrackingStep("ShouldNotRun"))
            .Build();

        var context = new WorkflowContext(cts.Token);

        // When
        var result = await workflow.ExecuteAsync(context);

        // Then
        result.Status.Should().Be(WorkflowStatus.Aborted);
    }
}
