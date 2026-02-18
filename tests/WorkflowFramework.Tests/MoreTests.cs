using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Diagnostics;
using WorkflowFramework.Extensions.Persistence;
using WorkflowFramework.Extensions.Persistence.InMemory;
using WorkflowFramework.Persistence;
using WorkflowFramework.Pipeline;
using WorkflowFramework.Registry;
using WorkflowFramework.Testing;
using WorkflowFramework.Tests.Common;
using WorkflowFramework.Validation;
using Xunit;
using PipelineFactory = WorkflowFramework.Pipeline.Pipeline;

namespace WorkflowFramework.Tests;

public class AuditMiddlewareExtendedTests
{
    [Fact]
    public async Task AuditMiddleware_RecordsCorrectTimestamps()
    {
        var store = new InMemoryAuditStore();
        var middleware = new AuditMiddleware(store);

        var workflow = Workflow.Create("AuditTiming")
            .Use(middleware)
            .Step("S1", async _ => await Task.Delay(10))
            .Build();

        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);

        var entries = store.AllEntries;
        entries.Should().HaveCount(1);
        entries[0].Duration.Should().NotBeNull();
        entries[0].Duration!.Value.TotalMilliseconds.Should().BeGreaterThan(0);
        entries[0].StartedAt.Should().BeBefore(entries[0].CompletedAt!.Value);
    }

    [Fact]
    public async Task InMemoryAuditStore_FiltersByWorkflowId()
    {
        var store = new InMemoryAuditStore();
        await store.RecordAsync(new AuditEntry { WorkflowId = "a", StepName = "S1" });
        await store.RecordAsync(new AuditEntry { WorkflowId = "b", StepName = "S2" });
        await store.RecordAsync(new AuditEntry { WorkflowId = "a", StepName = "S3" });

        var entries = await store.GetEntriesAsync("a");
        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.WorkflowId == "a");
    }
}

public class CachingMiddlewareExtendedTests
{
    [Fact]
    public async Task CachingMiddleware_ClearResetsCache()
    {
        var caching = new CachingMiddleware();
        var context = new WorkflowContext();
        var step = new FakeStep("test");

        var count = 0;
        await caching.InvokeAsync(context, step, _ => { count++; return Task.CompletedTask; });
        caching.Clear();
        await caching.InvokeAsync(context, step, _ => { count++; return Task.CompletedTask; });

        count.Should().Be(2); // After clear, should execute again
    }
}

public class IdempotencyExtendedTests
{
    [Fact]
    public async Task IdempotencyMiddleware_DifferentSteps_BothExecute()
    {
        var count = 0;
        var idempotency = new IdempotencyMiddleware();
        var context = new WorkflowContext();

        var step1 = new FakeStep("Step1");
        var step2 = new FakeStep("Step2");

        await idempotency.InvokeAsync(context, step1, _ => { count++; return Task.CompletedTask; });
        await idempotency.InvokeAsync(context, step2, _ => { count++; return Task.CompletedTask; });

        count.Should().Be(2);
    }
}

public class InMemoryPersistenceExtendedTests
{
    [Fact]
    public async Task InMemoryStore_ConcurrentAccess()
    {
        var store = new InMemoryWorkflowStateStore();
        var tasks = Enumerable.Range(0, 100).Select(i =>
        {
            var state = new WorkflowState
            {
                WorkflowId = $"wf-{i}",
                CorrelationId = "c",
                WorkflowName = "T",
                Timestamp = DateTimeOffset.UtcNow
            };
            return store.SaveCheckpointAsync($"wf-{i}", state);
        });

        await Task.WhenAll(tasks);
        store.GetAllStates().Count.Should().Be(100);
    }

    [Fact]
    public async Task InMemoryStore_GetAllStates_ReturnsAll()
    {
        var store = new InMemoryWorkflowStateStore();
        for (int i = 0; i < 3; i++)
        {
            await store.SaveCheckpointAsync($"wf-{i}", new WorkflowState
            {
                WorkflowId = $"wf-{i}",
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        store.GetAllStates().Count.Should().Be(3);
    }
}

public class PipelineExtendedTests
{
    [Fact]
    public async Task Pipeline_MultipleSteps_CancellationPropagated()
    {
        var tokens = new List<CancellationToken>();
        var pipeline = PipelineFactory.Create<int>()
            .Pipe<int>((i, ct) => { tokens.Add(ct); return Task.FromResult(i + 1); })
            .Pipe<int>((i, ct) => { tokens.Add(ct); return Task.FromResult(i + 1); })
            .Pipe<int>((i, ct) => { tokens.Add(ct); return Task.FromResult(i + 1); })
            .Build();

        using var cts = new CancellationTokenSource();
        var result = await pipeline(0, cts.Token);

        result.Should().Be(3);
        tokens.Should().HaveCount(3);
        tokens.Should().OnlyContain(t => t == cts.Token);
    }

    [Fact]
    public async Task Pipeline_WithNewStep_Works()
    {
        var pipeline = PipelineFactory.Create<int>()
            .Pipe<DoubleStep, int>()
            .Build();

        var result = await pipeline(5, CancellationToken.None);
        result.Should().Be(10);
    }

    private class DoubleStep : IPipelineStep<int, int>
    {
        public string Name => "Double";
        public Task<int> ExecuteAsync(int input, CancellationToken cancellationToken = default)
            => Task.FromResult(input * 2);
    }
}

public class CheckpointMiddlewareTests
{
    [Fact]
    public async Task CheckpointMiddleware_SavesAfterEachStep()
    {
        var store = new InMemoryWorkflowStateStore();
        var middleware = new CheckpointMiddleware(store);

        var workflow = Workflow.Create("ChkTest")
            .Use(middleware)
            .Step("A", _ => Task.CompletedTask)
            .Step("B", _ => Task.CompletedTask)
            .Step("C", _ => Task.CompletedTask)
            .Build();

        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);

        // Final checkpoint should reflect last step
        var state = await store.LoadCheckpointAsync(context.WorkflowId);
        state.Should().NotBeNull();
        state!.LastCompletedStepIndex.Should().Be(2);
    }
}

public class WorkflowBuilderNullTests
{
    [Fact]
    public void Step_NullStep_Throws()
    {
        var act = () => Workflow.Create().Step((IStep)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Use_NullMiddleware_Throws()
    {
        var act = () => Workflow.Create().Use((IWorkflowMiddleware)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithName_NullName_Throws()
    {
        var act = () => Workflow.Create().WithName(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithEvents_NullEvents_Throws()
    {
        var act = () => Workflow.Create().WithEvents(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

public class WorkflowEngineNullTests
{
    [Fact]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var workflow = Workflow.Create("test").Step("s", _ => Task.CompletedTask).Build();
        var act = () => workflow.ExecuteAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

public class TypedWorkflowExtendedTests
{
    public class Data { public int Counter { get; set; } }

    [Fact]
    public async Task TypedWorkflow_WithMiddleware()
    {
        var store = new InMemoryAuditStore();
        var workflow = Workflow.Create<Data>("TypedAudit")
            .Use(new AuditMiddleware(store))
            .Step("Inc", ctx => { ctx.Data.Counter++; return Task.CompletedTask; })
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext<Data>(new Data()));
        result.Data.Counter.Should().Be(1);
        store.AllEntries.Should().HaveCount(1);
    }

    [Fact]
    public async Task TypedWorkflow_Parallel()
    {
        var workflow = Workflow.Create<Data>("TypedParallel")
            .Step("Init", ctx => { ctx.Data.Counter = 0; return Task.CompletedTask; })
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext<Data>(new Data()));
        result.IsSuccess.Should().BeTrue();
    }
}

public class FakeStepTypedTests
{
    public class TestData { public string Value { get; set; } = ""; }

    [Fact]
    public async Task FakeTypedStep_TracksExecutions()
    {
        var fake = new FakeStep<TestData>("Test", ctx =>
        {
            ctx.Data.Value = "set";
            return Task.CompletedTask;
        });

        var context = new WorkflowContext<TestData>(new TestData());
        await fake.ExecuteAsync(context);

        fake.ExecutionCount.Should().Be(1);
        context.Data.Value.Should().Be("set");
    }

    [Fact]
    public async Task FakeTypedStep_NoOp_Works()
    {
        var fake = new FakeStep<TestData>("NoOp");
        await fake.ExecuteAsync(new WorkflowContext<TestData>(new TestData()));
        fake.ExecutionCount.Should().Be(1);
    }
}

public class WorkflowAbortTests
{
    [Fact]
    public async Task AbortedContext_SkipsRemainingSteps()
    {
        var ran = false;
        var workflow = Workflow.Create()
            .Step("Abort", ctx => { ctx.IsAborted = true; return Task.CompletedTask; })
            .Step("After", _ => { ran = true; return Task.CompletedTask; })
            .Build();

        var result = await workflow.ExecuteAsync(new WorkflowContext());
        result.Status.Should().Be(WorkflowStatus.Aborted);
        ran.Should().BeFalse();
    }
}

public class WorkflowResultTests
{
    [Fact]
    public void WorkflowResult_IsSuccess_OnlyForCompleted()
    {
        var context = new WorkflowContext();
        new WorkflowResult(WorkflowStatus.Completed, context).IsSuccess.Should().BeTrue();
        new WorkflowResult(WorkflowStatus.Faulted, context).IsSuccess.Should().BeFalse();
        new WorkflowResult(WorkflowStatus.Aborted, context).IsSuccess.Should().BeFalse();
        new WorkflowResult(WorkflowStatus.Compensated, context).IsSuccess.Should().BeFalse();
    }
}
