using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Extensions.Distributed;
using WorkflowFramework.Extensions.Diagnostics;
using WorkflowFramework.Extensions.Persistence.EntityFramework;
using WorkflowFramework.Extensions.Http;
using WorkflowFramework.Persistence;
using WorkflowFramework.Testing;
using Xunit;

namespace WorkflowFramework.Tests;

public class NewPackageTests
{
    // ==========================================
    // EF Core Persistence Tests
    // ==========================================

    private static WorkflowDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new WorkflowDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task EfCoreStore_SaveAndLoad_RoundTrips()
    {
        using var db = CreateInMemoryDbContext();
        var store = new EfCoreWorkflowStateStore(db);
        var state = new WorkflowState
        {
            WorkflowId = "wf-1",
            CorrelationId = "corr-1",
            WorkflowName = "Test",
            LastCompletedStepIndex = 2,
            Status = WorkflowStatus.Running,
            Timestamp = DateTimeOffset.UtcNow
        };

        await store.SaveCheckpointAsync("wf-1", state);
        var loaded = await store.LoadCheckpointAsync("wf-1");

        loaded.Should().NotBeNull();
        loaded!.WorkflowId.Should().Be("wf-1");
        loaded.CorrelationId.Should().Be("corr-1");
        loaded.LastCompletedStepIndex.Should().Be(2);
    }

    [Fact]
    public async Task EfCoreStore_Delete_RemovesState()
    {
        using var db = CreateInMemoryDbContext();
        var store = new EfCoreWorkflowStateStore(db);
        var state = new WorkflowState { WorkflowId = "wf-2", CorrelationId = "c", Timestamp = DateTimeOffset.UtcNow };

        await store.SaveCheckpointAsync("wf-2", state);
        await store.DeleteCheckpointAsync("wf-2");
        var loaded = await store.LoadCheckpointAsync("wf-2");

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task EfCoreStore_SaveTwice_Updates()
    {
        using var db = CreateInMemoryDbContext();
        var store = new EfCoreWorkflowStateStore(db);
        var state = new WorkflowState { WorkflowId = "wf-3", CorrelationId = "c", LastCompletedStepIndex = 1, Timestamp = DateTimeOffset.UtcNow };

        await store.SaveCheckpointAsync("wf-3", state);
        state.LastCompletedStepIndex = 5;
        await store.SaveCheckpointAsync("wf-3", state);

        var loaded = await store.LoadCheckpointAsync("wf-3");
        loaded!.LastCompletedStepIndex.Should().Be(5);
    }

    [Fact]
    public async Task EfCoreStore_LoadNonExistent_ReturnsNull()
    {
        using var db = CreateInMemoryDbContext();
        var store = new EfCoreWorkflowStateStore(db);

        var loaded = await store.LoadCheckpointAsync("nonexistent");
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task EfCoreStore_DeleteNonExistent_DoesNotThrow()
    {
        using var db = CreateInMemoryDbContext();
        var store = new EfCoreWorkflowStateStore(db);

        await store.Invoking(s => s.DeleteCheckpointAsync("nonexistent"))
            .Should().NotThrowAsync();
    }

    // ==========================================
    // Distributed Lock Tests
    // ==========================================

    [Fact]
    public async Task InMemoryLock_AcquireAndRelease()
    {
        var lockProvider = new InMemoryDistributedLock();
        var handle = await lockProvider.AcquireAsync("key1", TimeSpan.FromSeconds(30));

        handle.Should().NotBeNull();
        await handle!.DisposeAsync();
    }

    [Fact]
    public async Task InMemoryLock_DoubleAcquire_ReturnsNull()
    {
        var lockProvider = new InMemoryDistributedLock();
        var handle1 = await lockProvider.AcquireAsync("key2", TimeSpan.FromSeconds(30));
        var handle2 = await lockProvider.AcquireAsync("key2", TimeSpan.FromSeconds(30));

        handle1.Should().NotBeNull();
        handle2.Should().BeNull();
        await handle1!.DisposeAsync();
    }

    [Fact]
    public async Task InMemoryLock_AfterRelease_CanReacquire()
    {
        var lockProvider = new InMemoryDistributedLock();
        var handle1 = await lockProvider.AcquireAsync("key3", TimeSpan.FromSeconds(30));
        await handle1!.DisposeAsync();

        var handle2 = await lockProvider.AcquireAsync("key3", TimeSpan.FromSeconds(30));
        handle2.Should().NotBeNull();
        await handle2!.DisposeAsync();
    }

    // ==========================================
    // Distributed Queue Tests
    // ==========================================

    [Fact]
    public async Task InMemoryQueue_EnqueueDequeue()
    {
        var queue = new InMemoryWorkflowQueue();
        var item = new WorkflowQueueItem { WorkflowName = "TestWf" };

        await queue.EnqueueAsync(item);
        var dequeued = await queue.DequeueAsync();

        dequeued.Should().NotBeNull();
        dequeued!.WorkflowName.Should().Be("TestWf");
    }

    [Fact]
    public async Task InMemoryQueue_DequeueEmpty_ReturnsNull()
    {
        var queue = new InMemoryWorkflowQueue();
        var item = await queue.DequeueAsync();
        item.Should().BeNull();
    }

    [Fact]
    public async Task InMemoryQueue_GetLength()
    {
        var queue = new InMemoryWorkflowQueue();
        await queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "A" });
        await queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "B" });

        var length = await queue.GetLengthAsync();
        length.Should().Be(2);
    }

    [Fact]
    public async Task InMemoryQueue_FIFO_Order()
    {
        var queue = new InMemoryWorkflowQueue();
        await queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "First" });
        await queue.EnqueueAsync(new WorkflowQueueItem { WorkflowName = "Second" });

        var first = await queue.DequeueAsync();
        var second = await queue.DequeueAsync();

        first!.WorkflowName.Should().Be("First");
        second!.WorkflowName.Should().Be("Second");
    }

    // ==========================================
    // HTTP Step Tests
    // ==========================================

    [Fact]
    public void HttpStepOptions_Defaults()
    {
        var opts = new HttpStepOptions();
        opts.Method.Should().Be(HttpMethod.Get);
        opts.EnsureSuccessStatusCode.Should().BeTrue();
        opts.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void HttpStep_Name_UsesMethod()
    {
        var step = new HttpStep(new HttpStepOptions { Url = "http://example.com", Method = HttpMethod.Post });
        step.Name.Should().Be("HttpPOST");
    }

    [Fact]
    public void HttpStep_Name_UsesCustomName()
    {
        var step = new HttpStep(new HttpStepOptions { Url = "http://example.com", Name = "CallApi" });
        step.Name.Should().Be("CallApi");
    }

    [Fact]
    public void HttpBuilderExtensions_HttpGet_AddsStep()
    {
        var workflow = Workflow.Create("Test")
            .HttpGet("http://example.com")
            .Build();

        workflow.Steps.Should().HaveCount(1);
        workflow.Steps[0].Name.Should().Contain("HttpGet");
    }

    [Fact]
    public void HttpBuilderExtensions_HttpPost_AddsStep()
    {
        var workflow = Workflow.Create("Test")
            .HttpPost("http://example.com", "{}")
            .Build();

        workflow.Steps.Should().HaveCount(1);
    }

    // ==========================================
    // Metrics Middleware Tests
    // ==========================================

    [Fact]
    public async Task MetricsMiddleware_TracksStepCount()
    {
        var metrics = new MetricsMiddleware();
        var workflow = Workflow.Create("Test")
            .Use(metrics)
            .Step("A", _ => Task.CompletedTask)
            .Step("B", _ => Task.CompletedTask)
            .Build();

        await workflow.ExecuteAsync(new WorkflowContext());

        metrics.TotalSteps.Should().Be(2);
        metrics.FailedSteps.Should().Be(0);
    }

    [Fact]
    public async Task MetricsMiddleware_TracksFailures()
    {
        var metrics = new MetricsMiddleware();
        var workflow = Workflow.Create("Test")
            .Use(metrics)
            .Step("Fail", _ => throw new InvalidOperationException("boom"))
            .Build();

        await workflow.ExecuteAsync(new WorkflowContext());

        metrics.TotalSteps.Should().Be(1);
        metrics.FailedSteps.Should().Be(1);
    }

    [Fact]
    public async Task MetricsMiddleware_AverageDuration_NotZero()
    {
        var metrics = new MetricsMiddleware();
        var workflow = Workflow.Create("Test")
            .Use(metrics)
            .Step("A", _ => Task.CompletedTask)
            .Build();

        await workflow.ExecuteAsync(new WorkflowContext());
        metrics.AverageDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    // ==========================================
    // Core Expansion Tests
    // ==========================================

    [Fact]
    public void StepBase_Name_UsesTypeName()
    {
        var step = new TestStepBase();
        step.Name.Should().Be("TestStepBase");
    }

    [Fact]
    public void StepBase_Name_UsesAttribute()
    {
        var step = new NamedTestStep();
        step.Name.Should().Be("CustomName");
    }

    [Fact]
    public void WorkflowOptions_Defaults()
    {
        var opts = new WorkflowOptions();
        opts.MaxParallelism.Should().BeGreaterThan(0);
        opts.DefaultTimeout.Should().BeNull();
        opts.EnableCompensation.Should().BeFalse();
        opts.DefaultMaxRetryAttempts.Should().Be(3);
    }

    [Fact]
    public void WorkflowException_HasMessage()
    {
        var ex = new WorkflowException("test");
        ex.Message.Should().Be("test");
    }

    [Fact]
    public void WorkflowAbortedException_HasWorkflowId()
    {
        var ex = new WorkflowAbortedException("wf-1");
        ex.WorkflowId.Should().Be("wf-1");
        ex.Message.Should().Contain("wf-1");
    }

    [Fact]
    public void StepExecutionException_HasStepName()
    {
        var inner = new InvalidOperationException("bad");
        var ex = new StepExecutionException("MyStep", inner);
        ex.StepName.Should().Be("MyStep");
        ex.InnerException.Should().Be(inner);
    }

    // ==========================================
    // Testing Expansion Tests
    // ==========================================

    [Fact]
    public async Task WorkflowAssertions_ShouldBeCompleted()
    {
        var workflow = Workflow.Create("Test").Step("A", _ => Task.CompletedTask).Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        result.ShouldBeCompleted();
    }

    [Fact]
    public async Task WorkflowAssertions_ShouldBeFaulted()
    {
        var workflow = Workflow.Create("Test").Step("A", _ => throw new Exception("fail")).Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        result.ShouldBeFaulted();
    }

    [Fact]
    public async Task WorkflowAssertions_ShouldHaveProperty()
    {
        var workflow = Workflow.Create("Test")
            .Step("A", ctx => { ctx.Properties["key"] = "value"; return Task.CompletedTask; })
            .Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        result.ShouldHaveProperty("key");
        result.ShouldHaveProperty("key", "value");
    }

    [Fact]
    public async Task WorkflowAssertions_ShouldHaveNoErrors()
    {
        var workflow = Workflow.Create("Test").Step("A", _ => Task.CompletedTask).Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());

        result.ShouldHaveNoErrors();
    }

    [Fact]
    public async Task MockStep_RecordsInvocations()
    {
        var mock = new MockStep("Test");
        var workflow = Workflow.Create("Test").Step(mock).Build();

        await workflow.ExecuteAsync(new WorkflowContext());

        mock.InvocationCount.Should().Be(1);
        mock.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public async Task MockStep_ThrowsConfiguredException()
    {
        var mock = new MockStep("Test", throwException: new InvalidOperationException("boom"));
        var workflow = Workflow.Create("Test").Step(mock).Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());
        result.Status.Should().Be(WorkflowStatus.Faulted);
    }

    [Fact]
    public async Task WorkflowTestBuilder_ExecutesWithProperties()
    {
        var workflow = Workflow.Create("Test")
            .Step("Check", ctx =>
            {
                ctx.Properties["found"] = ctx.Properties.ContainsKey("input");
                return Task.CompletedTask;
            })
            .Build();

        var result = await new WorkflowTestBuilder()
            .WithProperty("input", 42)
            .ExecuteAsync(workflow);

        result.ShouldBeCompleted();
        result.ShouldHaveProperty("found", true);
    }

    [Fact]
    public async Task WorkflowTestBuilder_WithStepOverride()
    {
        var workflow = Workflow.Create("Test")
            .Step("Original", ctx => { ctx.Properties["ran"] = "original"; return Task.CompletedTask; })
            .Build();

        var result = await new WorkflowTestBuilder()
            .WithStepOverride("Original", new FakeStep("Original", ctx => { ctx.Properties["ran"] = "override"; return Task.CompletedTask; }))
            .ExecuteAsync(workflow);

        result.ShouldHaveProperty("ran", "override");
    }

    // ==========================================
    // WorkflowDbContext Model Tests
    // ==========================================

    [Fact]
    public void WorkflowStateEntity_Properties()
    {
        var entity = new WorkflowStateEntity
        {
            WorkflowId = "id",
            CorrelationId = "corr",
            WorkflowName = "test",
            LastCompletedStepIndex = 3,
            Status = 1,
            PropertiesJson = "{}",
            Timestamp = DateTimeOffset.UtcNow
        };

        entity.WorkflowId.Should().Be("id");
        entity.CorrelationId.Should().Be("corr");
        entity.WorkflowName.Should().Be("test");
        entity.LastCompletedStepIndex.Should().Be(3);
        entity.Status.Should().Be(1);
    }

    [Fact]
    public void WorkflowQueueItem_Defaults()
    {
        var item = new WorkflowQueueItem();
        item.Id.Should().NotBeNullOrEmpty();
        item.EnqueuedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ==========================================
    // IWorkflowSerializer Interface Tests
    // ==========================================

    [Fact]
    public void IWorkflowSerializer_CanBeImplemented()
    {
        IWorkflowSerializer serializer = new TestSerializer();
        var result = serializer.Serialize(42);
        result.Should().Be("42");
        serializer.Deserialize<int>(result).Should().Be(42);
    }

    // Helper classes

    private class TestStepBase : StepBase
    {
        public override Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    [WorkflowFramework.Attributes.StepName("CustomName")]
    private class NamedTestStep : StepBase
    {
        public override Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private class TestSerializer : IWorkflowSerializer
    {
        public string Serialize<T>(T value) => value?.ToString() ?? "";
        public T? Deserialize<T>(string data) => (T)Convert.ChangeType(data, typeof(T));
    }
}
