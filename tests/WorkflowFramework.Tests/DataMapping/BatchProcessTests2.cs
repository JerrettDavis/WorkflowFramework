using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Batch;
using Xunit;

namespace WorkflowFramework.Tests.DataMapping;

public class BatchProcessExtendedTests
{
    [Fact]
    public void BatchProcessStep_NullProcessBatch_Throws()
    {
        var act = () => new BatchProcessStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task BatchProcessStep_MissingItems_Throws()
    {
        var step = new BatchProcessStep((batch, ctx) => Task.CompletedTask);
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task BatchProcessStep_Sequential_ProcessesAllBatches()
    {
        var batchCount = 0;
        var step = new BatchProcessStep(
            (batch, ctx) => { batchCount++; return Task.CompletedTask; },
            new BatchOptions { BatchSize = 2, MaxConcurrency = 1 });
        var context = new WorkflowContext();
        context.Properties[BatchProcessStep.BatchItemsKey] = new object[] { 1, 2, 3, 4, 5 }.AsEnumerable();
        await step.ExecuteAsync(context);
        batchCount.Should().Be(3); // 2+2+1
    }

    [Fact]
    public async Task BatchProcessStep_Parallel_ProcessesAllBatches()
    {
        var batchCount = 0;
        var step = new BatchProcessStep(
            (batch, ctx) => { Interlocked.Increment(ref batchCount); return Task.CompletedTask; },
            new BatchOptions { BatchSize = 2, MaxConcurrency = 4 });
        var context = new WorkflowContext();
        context.Properties[BatchProcessStep.BatchItemsKey] = new object[] { 1, 2, 3, 4 }.AsEnumerable();
        await step.ExecuteAsync(context);
        batchCount.Should().Be(2);
    }

    [Fact]
    public async Task BatchProcessStep_Cancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var step = new BatchProcessStep((batch, ctx) => Task.CompletedTask, new BatchOptions { BatchSize = 1 });
        var context = new WorkflowContext(cts.Token);
        context.Properties[BatchProcessStep.BatchItemsKey] = new object[] { 1, 2 }.AsEnumerable();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void BatchOptions_Defaults()
    {
        var opts = new BatchOptions();
        opts.BatchSize.Should().Be(100);
        opts.MaxConcurrency.Should().Be(1);
    }

    [Fact]
    public void DataBatcher_NullItems_Throws()
    {
        var batcher = new DataBatcher();
        var act = () => batcher.Batch<int>(null!, 10).ToList();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DataBatcher_ZeroBatchSize_Throws()
    {
        var batcher = new DataBatcher();
        var act = () => batcher.Batch(new[] { 1 }, 0).ToList();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DataBatcher_BatchesCorrectly()
    {
        var batcher = new DataBatcher();
        var batches = batcher.Batch(new[] { 1, 2, 3, 4, 5 }, 2).ToList();
        batches.Should().HaveCount(3);
        batches[0].Should().Equal(1, 2);
        batches[1].Should().Equal(3, 4);
        batches[2].Should().Equal(5);
    }

    [Fact]
    public void DataBatcher_EmptyCollection_ReturnsEmpty()
    {
        var batcher = new DataBatcher();
        var batches = batcher.Batch(Array.Empty<int>(), 10).ToList();
        batches.Should().BeEmpty();
    }

    [Fact]
    public void DataBatcher_BatchSizeLargerThanCollection()
    {
        var batcher = new DataBatcher();
        var batches = batcher.Batch(new[] { 1, 2 }, 100).ToList();
        batches.Should().HaveCount(1);
        batches[0].Should().Equal(1, 2);
    }
}
