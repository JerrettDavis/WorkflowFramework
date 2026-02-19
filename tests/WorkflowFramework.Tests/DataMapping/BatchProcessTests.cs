using Xunit;
using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Batch;

namespace WorkflowFramework.Tests.DataMapping;

public class BatchProcessTests
{
    [Fact]
    public async Task BatchProcessStep_ProcessesAllBatches()
    {
        var processedBatches = new List<int>();
        var step = new BatchProcessStep(
            (batch, _) =>
            {
                processedBatches.Add(batch.Count);
                return Task.CompletedTask;
            },
            new BatchOptions { BatchSize = 3 });

        var context = new WorkflowContext();
        context.Properties[BatchProcessStep.BatchItemsKey] = Enumerable.Range(1, 7).Cast<object>();

        await step.ExecuteAsync(context);

        processedBatches.Should().Equal(3, 3, 1);
    }

    [Fact]
    public async Task BatchProcessStep_ParallelExecution()
    {
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        var step = new BatchProcessStep(
            async (_, _) =>
            {
                lock (lockObj)
                {
                    concurrentCount++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                }
                await Task.Delay(50);
                lock (lockObj) concurrentCount--;
            },
            new BatchOptions { BatchSize = 1, MaxConcurrency = 3 });

        var context = new WorkflowContext();
        context.Properties[BatchProcessStep.BatchItemsKey] = Enumerable.Range(1, 6).Cast<object>();

        await step.ExecuteAsync(context);

        maxConcurrent.Should().BeGreaterThan(1);
    }

    [Fact]
    public void DataBatcher_SplitsCorrectly()
    {
        var batcher = new DataBatcher();
        var items = Enumerable.Range(1, 10);
        var batches = batcher.Batch(items, 3).ToList();
        batches.Should().HaveCount(4);
        batches[0].Should().Equal(1, 2, 3);
        batches[3].Should().Equal(10);
    }
}
