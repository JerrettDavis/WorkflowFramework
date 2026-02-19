using FluentAssertions;
using Microsoft.Extensions.Logging;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Diagnostics;

public class StructuredLoggingMiddlewareTests
{
    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        FluentActions.Invoking(() => new StructuredLoggingMiddleware(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_LogsStartAndComplete()
    {
        var logger = new FakeLogger();
        var mw = new StructuredLoggingMiddleware(logger);
        await mw.InvokeAsync(CreateCtx(), new S("Step1"), _ => Task.CompletedTask);
        logger.Entries.Should().HaveCountGreaterThanOrEqualTo(2);
        logger.Entries.Should().Contain(e => e.Contains("starting"));
        logger.Entries.Should().Contain(e => e.Contains("completed"));
    }

    [Fact]
    public async Task InvokeAsync_OnError_LogsError()
    {
        var logger = new FakeLogger();
        var mw = new StructuredLoggingMiddleware(logger);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mw.InvokeAsync(CreateCtx(), new S("Bad"), _ => throw new InvalidOperationException("boom")));
        logger.Entries.Should().Contain(e => e.Contains("failed"));
    }

    [Fact]
    public async Task InvokeAsync_CreatesScopeWithCorrelation()
    {
        var logger = new FakeLogger();
        var mw = new StructuredLoggingMiddleware(logger);
        await mw.InvokeAsync(CreateCtx(), new S("X"), _ => Task.CompletedTask);
        logger.ScopeCreated.Should().BeTrue();
    }

    private static TestCtx CreateCtx() => new();
    private class S(string n) : IStep
    {
        public string Name { get; } = n;
        public Task ExecuteAsync(IWorkflowContext c) => Task.CompletedTask; }
    private class TestCtx : IWorkflowContext
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
        public bool ScopeCreated { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull { ScopeCreated = true; return new Noop(); }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(formatter(state, exception));
        }
        private class Noop : IDisposable { public void Dispose() { } }
    }
}
