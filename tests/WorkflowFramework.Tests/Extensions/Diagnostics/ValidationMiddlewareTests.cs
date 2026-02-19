using FluentAssertions;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Diagnostics;

public class ValidationMiddlewareTests
{
    [Fact]
    public void Constructor_NullValidator_Throws()
    {
        FluentActions.Invoking(() => new ValidationMiddleware(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Valid_ProceedsToNext()
    {
        var mw = new ValidationMiddleware((_, _) => Task.FromResult(true));
        var executed = false;
        await mw.InvokeAsync(Ctx(), Step("S"), _ => { executed = true; return Task.CompletedTask; });
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Invalid_ThrowsInvalidOperation()
    {
        var mw = new ValidationMiddleware((_, _) => Task.FromResult(false));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mw.InvokeAsync(Ctx(), Step("S"), _ => Task.CompletedTask));
    }

    [Fact]
    public async Task Invalid_MessageContainsStepName()
    {
        var mw = new ValidationMiddleware((_, _) => Task.FromResult(false));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mw.InvokeAsync(Ctx(), Step("MyStep"), _ => Task.CompletedTask));
        ex.Message.Should().Contain("MyStep");
    }

    private static IWorkflowContext Ctx() => new C();
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
