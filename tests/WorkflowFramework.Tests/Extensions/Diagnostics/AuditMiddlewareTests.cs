using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Diagnostics;

public class AuditMiddlewareTests
{
    [Fact]
    public void Constructor_NullStore_Throws()
    {
        FluentActions.Invoking(() => new AuditMiddleware(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_RecordsCompletedEntry()
    {
        var store = new InMemoryAuditStore();
        var mw = new AuditMiddleware(store);
        var ctx = Ctx("wf1");
        await mw.InvokeAsync(ctx, Step("S1"), _ => Task.CompletedTask);
        var entries = await store.GetEntriesAsync("wf1");
        entries.Should().HaveCount(1);
        entries[0].StepName.Should().Be("S1");
        entries[0].Status.Should().Be(AuditStatus.Completed);
        entries[0].ErrorMessage.Should().BeNull();
        entries[0].Duration.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_OnError_RecordsFailedEntry()
    {
        var store = new InMemoryAuditStore();
        var mw = new AuditMiddleware(store);
        var ctx = Ctx("wf2");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mw.InvokeAsync(ctx, Step("Bad"), _ => throw new InvalidOperationException("err")));
        var entries = await store.GetEntriesAsync("wf2");
        entries.Should().HaveCount(1);
        entries[0].Status.Should().Be(AuditStatus.Failed);
        entries[0].ErrorMessage.Should().Be("err");
    }

    [Fact]
    public async Task InMemoryAuditStore_AllEntries()
    {
        var store = new InMemoryAuditStore();
        await store.RecordAsync(new AuditEntry { WorkflowId = "a" });
        await store.RecordAsync(new AuditEntry { WorkflowId = "b" });
        store.AllEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task InMemoryAuditStore_GetEntries_FiltersById()
    {
        var store = new InMemoryAuditStore();
        await store.RecordAsync(new AuditEntry { WorkflowId = "a" });
        await store.RecordAsync(new AuditEntry { WorkflowId = "b" });
        (await store.GetEntriesAsync("a")).Should().HaveCount(1);
    }

    [Fact]
    public void AuditEntry_Defaults()
    {
        var e = new AuditEntry();
        e.WorkflowId.Should().BeEmpty();
        e.CompletedAt.Should().BeNull();
        e.Duration.Should().BeNull();
    }

    [Fact]
    public void AuditEntry_Duration_WhenCompleted()
    {
        var e = new AuditEntry
        {
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        e.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void AuditStatus_Values()
    {
        Enum.GetValues<AuditStatus>().Should().HaveCount(2);
    }

    private static IWorkflowContext Ctx(string wfId = "w") => new C { WorkflowId = wfId };
    private static IStep Step(string n = "S") => new St(n);
    private class St(string n) : IStep
    {
        public string Name { get; } = n;
        public Task ExecuteAsync(IWorkflowContext c) => Task.CompletedTask; }
    private class C : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}
