using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Polly.Tests.Support;

/// <summary>
/// Base class for all Extensions.Polly TinyBDD scenario classes.
/// </summary>
public abstract class PollyTestBase : TinyBddXunitBase
{
    protected PollyTestBase(ITestOutputHelper output) : base(output) { }
}

/// <summary>
/// A simple delegate-based middleware for composition tests.
/// </summary>
public sealed class DelegateMiddleware(
    Func<IWorkflowContext, IStep, StepDelegate, Task> impl) : IWorkflowMiddleware
{
    public Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
        => impl(context, step, next);
}
