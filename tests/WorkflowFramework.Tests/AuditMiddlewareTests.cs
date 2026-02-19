using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests;

public class AuditMiddlewareTests
{
    [Fact]
    public async Task AuditMiddleware_RecordsStepExecution()
    {
        var store = new InMemoryAuditStore();
        var middleware = new AuditMiddleware(store);

        var workflow = Workflow.Create("AuditTest")
            .Use(middleware)
            .Step("StepA", _ => Task.CompletedTask)
            .Step("StepB", _ => Task.CompletedTask)
            .Build();

        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);

        var entries = await store.GetEntriesAsync(context.WorkflowId);
        entries.Should().HaveCount(2);
        entries[0].StepName.Should().Be("StepA");
        entries[0].Status.Should().Be(AuditStatus.Completed);
        entries[0].Duration.Should().NotBeNull();
        entries[1].StepName.Should().Be("StepB");
    }

    [Fact]
    public async Task AuditMiddleware_RecordsFailure()
    {
        var store = new InMemoryAuditStore();
        var middleware = new AuditMiddleware(store);

        var workflow = Workflow.Create("FailTest")
            .Use(middleware)
            .Step("FailStep", _ => throw new InvalidOperationException("Boom"))
            .Build();

        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);

        store.AllEntries.Should().ContainSingle();
        store.AllEntries[0].Status.Should().Be(AuditStatus.Failed);
        store.AllEntries[0].ErrorMessage.Should().Be("Boom");
    }

    [Fact]
    public async Task CachingMiddleware_SkipsDuplicateExecution()
    {
        var count = 0;
        var caching = new CachingMiddleware();

        var step = new FakeStepImpl("Cached", () => count++);
        var context = new WorkflowContext();

        // Execute same step twice through middleware
        await caching.InvokeAsync(context, step, _ => { count++; return Task.CompletedTask; });
        await caching.InvokeAsync(context, step, _ => { count++; return Task.CompletedTask; });

        count.Should().Be(1); // Only executed once
    }

    [Fact]
    public async Task IdempotencyMiddleware_PreventsDuplicates()
    {
        var count = 0;
        var idempotency = new IdempotencyMiddleware();

        var step = new FakeStepImpl("Idem", () => count++);
        var context = new WorkflowContext();

        await idempotency.InvokeAsync(context, step, _ => { count++; return Task.CompletedTask; });
        await idempotency.InvokeAsync(context, step, _ => { count++; return Task.CompletedTask; });

        count.Should().Be(1);
    }

    private class FakeStepImpl(string name, Action action) : IStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) { action(); return Task.CompletedTask; }
    }
}
