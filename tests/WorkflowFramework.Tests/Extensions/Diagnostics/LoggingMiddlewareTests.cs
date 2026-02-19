using FluentAssertions;
using Microsoft.Extensions.Logging;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Diagnostics;

public class LoggingMiddlewareTests
{
    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        FluentActions.Invoking(() => new LoggingMiddleware(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_LogsStartAndComplete()
    {
        var logger = new FakeLogger();
        var mw = new LoggingMiddleware(logger);
        await mw.InvokeAsync(Ctx(), Step("S1"), _ => Task.CompletedTask);
        logger.Entries.Should().Contain(e => e.Contains("starting"));
        logger.Entries.Should().Contain(e => e.Contains("completed"));
    }

    [Fact]
    public async Task InvokeAsync_OnError_LogsError()
    {
        var logger = new FakeLogger();
        var mw = new LoggingMiddleware(logger);
        await Assert.ThrowsAsync<Exception>(() =>
            mw.InvokeAsync(Ctx(), Step("Bad"), _ => throw new Exception("boom")));
        logger.Entries.Should().Contain(e => e.Contains("failed"));
    }

    [Fact]
    public async Task InvokeAsync_LogsContainStepName()
    {
        var logger = new FakeLogger();
        var mw = new LoggingMiddleware(logger);
        await mw.InvokeAsync(Ctx(), Step("MyStep"), _ => Task.CompletedTask);
        logger.Entries.Should().Contain(e => e.Contains("MyStep"));
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

    private class FakeLogger : ILogger
    {
        public List<string> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => new Noop();
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(formatter(state, exception));
        private class Noop : IDisposable { public void Dispose() { } }
    }
}
