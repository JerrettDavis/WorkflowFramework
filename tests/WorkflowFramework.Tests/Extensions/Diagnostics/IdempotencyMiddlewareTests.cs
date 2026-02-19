using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Diagnostics;

public class IdempotencyMiddlewareTests
{
    [Fact]
    public async Task FirstCall_Executes()
    {
        var mw = new IdempotencyMiddleware();
        var executed = false;
        await mw.InvokeAsync(Ctx(), Step("S1"), _ => { executed = true; return Task.CompletedTask; });
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task DuplicateCall_Skips()
    {
        var mw = new IdempotencyMiddleware();
        var count = 0;
        var ctx = Ctx();
        await mw.InvokeAsync(ctx, Step("S1"), _ => { count++; return Task.CompletedTask; });
        await mw.InvokeAsync(ctx, Step("S1"), _ => { count++; return Task.CompletedTask; });
        count.Should().Be(1);
    }

    [Fact]
    public async Task DifferentStepIndex_BothExecute()
    {
        var mw = new IdempotencyMiddleware();
        var count = 0;
        var ctx = Ctx();
        await mw.InvokeAsync(ctx, Step("S1"), _ => { count++; return Task.CompletedTask; });
        ctx.CurrentStepIndex = 1;
        await mw.InvokeAsync(ctx, Step("S1"), _ => { count++; return Task.CompletedTask; });
        count.Should().Be(2);
    }

    private static C Ctx() => new();
    private static IStep Step(string n) => new St(n);
    private class St : IStep { public St(string n) { Name = n; } public string Name { get; } public Task ExecuteAsync(IWorkflowContext c) => Task.CompletedTask; }
    private class C : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}
