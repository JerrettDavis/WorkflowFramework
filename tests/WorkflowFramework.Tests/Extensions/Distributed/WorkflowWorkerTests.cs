using FluentAssertions;
using WorkflowFramework.Extensions.Distributed;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Distributed;

public class WorkflowWorkerTests
{
    [Fact]
    public void Constructor_NullQueue_Throws()
    {
        FluentActions.Invoking(() => new WorkflowWorker(null!, (_, _) => Task.CompletedTask))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullHandler_Throws()
    {
        FluentActions.Invoking(() => new WorkflowWorker(new InMemoryWorkflowQueue(), null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkerId_IsUnique()
    {
        var q = new InMemoryWorkflowQueue();
        var w1 = new WorkflowWorker(q, (_, _) => Task.CompletedTask);
        var w2 = new WorkflowWorker(q, (_, _) => Task.CompletedTask);
        w1.WorkerId.Should().NotBe(w2.WorkerId);
    }

    [Fact]
    public void IsRunning_InitiallyFalse()
    {
        var w = new WorkflowWorker(new InMemoryWorkflowQueue(), (_, _) => Task.CompletedTask);
        w.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAndStop()
    {
        var q = new InMemoryWorkflowQueue();
        await using var w = new WorkflowWorker(q, (_, _) => Task.CompletedTask,
            new WorkflowWorkerOptions { PollingInterval = TimeSpan.FromMilliseconds(20) });
        w.Start();
        w.IsRunning.Should().BeTrue();
        await w.StopAsync();
        w.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Start_Twice_IsIdempotent()
    {
        var q = new InMemoryWorkflowQueue();
        await using var w = new WorkflowWorker(q, (_, _) => Task.CompletedTask,
            new WorkflowWorkerOptions { PollingInterval = TimeSpan.FromMilliseconds(20) });
        w.Start();
        w.Start(); // should not throw
        w.IsRunning.Should().BeTrue();
        await w.StopAsync();
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_DoesNothing()
    {
        var w = new WorkflowWorker(new InMemoryWorkflowQueue(), (_, _) => Task.CompletedTask);
        await w.StopAsync(); // should not throw
    }

    [Fact]
    public async Task ProcessesEnqueuedItems()
    {
        var q = new InMemoryWorkflowQueue();
        await q.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "X" });
        var processed = new List<string>();
        await using var w = new WorkflowWorker(q, (item, _) =>
        {
            processed.Add(item.WorkflowName);
            return Task.CompletedTask;
        }, new WorkflowWorkerOptions { PollingInterval = TimeSpan.FromMilliseconds(20) });
        w.Start();
        await Task.Delay(150);
        await w.StopAsync();
        processed.Should().Contain("X");
        w.ProcessedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FailedHandler_IncrementsFailedCount()
    {
        var q = new InMemoryWorkflowQueue();
        await q.EnqueueAsync(new WorkflowQueueItem());
        await using var w = new WorkflowWorker(q, (_, _) => throw new Exception("boom"),
            new WorkflowWorkerOptions { PollingInterval = TimeSpan.FromMilliseconds(20) });
        w.Start();
        await Task.Delay(150);
        await w.StopAsync();
        w.FailedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetHealthStatus_ReturnsCorrectInfo()
    {
        var w = new WorkflowWorker(new InMemoryWorkflowQueue(), (_, _) => Task.CompletedTask);
        var h = w.GetHealthStatus();
        h.WorkerId.Should().Be(w.WorkerId);
        h.IsRunning.Should().BeFalse();
        h.ProcessedCount.Should().Be(0);
        h.FailedCount.Should().Be(0);
        h.LastCheckTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DisposeAsync_StopsWorker()
    {
        var w = new WorkflowWorker(new InMemoryWorkflowQueue(), (_, _) => Task.CompletedTask,
            new WorkflowWorkerOptions { PollingInterval = TimeSpan.FromMilliseconds(20) });
        w.Start();
        await w.DisposeAsync();
        w.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void WorkflowWorkerOptions_Defaults()
    {
        var o = new WorkflowWorkerOptions();
        o.PollingInterval.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void WorkerHealthStatus_Defaults()
    {
        var h = new WorkerHealthStatus();
        h.WorkerId.Should().BeEmpty();
        h.IsRunning.Should().BeFalse();
    }
}
