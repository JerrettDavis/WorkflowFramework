using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Diagnostics;

public class CachingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_FirstCall_Executes()
    {
        var mw = new CachingMiddleware();
        var executed = false;
        await mw.InvokeAsync(Ctx(), Step("S1"), _ => { executed = true; return Task.CompletedTask; });
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SecondCall_SameStep_Skips()
    {
        var mw = new CachingMiddleware();
        var count = 0;
        var ctx = Ctx();
        await mw.InvokeAsync(ctx, Step("S1"), _ => { count++; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, Step("S1"), _ => { count++; return Task.CompletedTask; });
        count.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_DifferentSteps_BothExecute()
    {
        var mw = new CachingMiddleware();
        var count = 0;
        var ctx = Ctx();
        await mw.InvokeAsync(ctx, Step("S1"), _ => { count++; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, Step("S2"), _ => { count++; return Task.CompletedTask; });
        count.Should().Be(2);
    }

    [Fact]
    public async Task Clear_AllowsReExecution()
    {
        var mw = new CachingMiddleware();
        var count = 0;
        var ctx = Ctx();
        await mw.InvokeAsync(ctx, Step("S1"), _ => { count++; return Task.CompletedTask; });
        mw.Clear();
        await mw.InvokeAsync(ctx, Step("S1"), _ => { count++; return Task.CompletedTask; });
        count.Should().Be(2);
    }

    private static IWorkflowContext Ctx() => new C();
    private static IStep Step(string n) => new St(n);
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
