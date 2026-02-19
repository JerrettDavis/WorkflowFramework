using WorkflowFramework.Internal;

namespace WorkflowFramework.Builder;

/// <summary>
/// Extension methods for <see cref="IWorkflowBuilder"/> adding looping, error handling, timeouts, and more.
/// </summary>
public static class WorkflowBuilderExtensions
{
    /// <summary>
    /// Adds a ForEach loop step that iterates over a collection.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="itemsSelector">A function to select the items collection from the context.</param>
    /// <param name="configure">A delegate to configure the loop body.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder ForEach<TItem>(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, IEnumerable<TItem>> itemsSelector,
        Action<IWorkflowBuilder> configure)
    {
        var bodyBuilder = new WorkflowBuilder();
        configure(bodyBuilder);
        var body = bodyBuilder.Build();
        var step = new ForEachStep<TItem>("ForEach", itemsSelector, body.Steps.ToArray());
        return builder.Step(step);
    }

    /// <summary>
    /// Adds a While loop step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="condition">The loop condition.</param>
    /// <param name="configure">A delegate to configure the loop body.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder While(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, bool> condition,
        Action<IWorkflowBuilder> configure)
    {
        var bodyBuilder = new WorkflowBuilder();
        configure(bodyBuilder);
        var body = bodyBuilder.Build();
        var step = new WhileStep("While", condition, body.Steps.ToArray());
        return builder.Step(step);
    }

    /// <summary>
    /// Adds a DoWhile loop step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="configure">A delegate to configure the loop body.</param>
    /// <param name="condition">The loop condition evaluated after each iteration.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder DoWhile(
        this IWorkflowBuilder builder,
        Action<IWorkflowBuilder> configure,
        Func<IWorkflowContext, bool> condition)
    {
        var bodyBuilder = new WorkflowBuilder();
        configure(bodyBuilder);
        var body = bodyBuilder.Build();
        var step = new DoWhileStep("DoWhile", body.Steps.ToArray(), condition);
        return builder.Step(step);
    }

    /// <summary>
    /// Adds a retry group that retries all contained steps on failure.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="configure">A delegate to configure the steps to retry.</param>
    /// <param name="maxAttempts">The maximum number of attempts.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder Retry(
        this IWorkflowBuilder builder,
        Action<IWorkflowBuilder> configure,
        int maxAttempts)
    {
        var bodyBuilder = new WorkflowBuilder();
        configure(bodyBuilder);
        var body = bodyBuilder.Build();
        var step = new RetryGroupStep("Retry", body.Steps.ToArray(), maxAttempts);
        return builder.Step(step);
    }

    /// <summary>
    /// Begins a try/catch/finally block.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="configure">A delegate to configure the try body.</param>
    /// <returns>A catch builder for adding catch handlers.</returns>
    public static ITryCatchBuilder Try(
        this IWorkflowBuilder builder,
        Action<IWorkflowBuilder> configure)
    {
        var bodyBuilder = new WorkflowBuilder();
        configure(bodyBuilder);
        var body = bodyBuilder.Build();
        return new TryCatchBuilderImpl(builder, body.Steps.ToArray());
    }

    /// <summary>
    /// Adds a sub-workflow step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="subWorkflow">The sub-workflow to execute.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder SubWorkflow(
        this IWorkflowBuilder builder,
        IWorkflow subWorkflow)
    {
        return builder.Step(new SubWorkflowStep(subWorkflow));
    }

    /// <summary>
    /// Adds a delay step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="delay">The duration to delay.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder Delay(
        this IWorkflowBuilder builder,
        TimeSpan delay)
    {
        return builder.Step(new DelayStep(delay));
    }

    /// <summary>
    /// Wraps the previously added step with a timeout.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder WithTimeout(
        this IWorkflowBuilder builder,
        TimeSpan timeout)
    {
        // This adds a timeout wrapper step
        return builder.Use(new TimeoutMiddleware(timeout));
    }
}

/// <summary>
/// Builder for try/catch/finally blocks.
/// </summary>
public interface ITryCatchBuilder
{
    /// <summary>
    /// Adds a catch handler for a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The exception type to catch.</typeparam>
    /// <param name="handler">The handler to execute when the exception is caught.</param>
    /// <returns>This builder for chaining.</returns>
    ITryCatchBuilder Catch<TException>(Func<IWorkflowContext, Exception, Task> handler) where TException : Exception;

    /// <summary>
    /// Adds a finally block.
    /// </summary>
    /// <param name="configure">A delegate to configure the finally body.</param>
    /// <returns>The parent workflow builder.</returns>
    IWorkflowBuilder Finally(Action<IWorkflowBuilder> configure);

    /// <summary>
    /// Ends the try/catch block without a finally.
    /// </summary>
    /// <returns>The parent workflow builder.</returns>
    IWorkflowBuilder EndTry();
}

internal sealed class TryCatchBuilderImpl(IWorkflowBuilder parent, IStep[] tryBody) : ITryCatchBuilder
{
    private readonly Dictionary<Type, Func<IWorkflowContext, Exception, Task>> _catchHandlers = new();

    public ITryCatchBuilder Catch<TException>(Func<IWorkflowContext, Exception, Task> handler) where TException : Exception
    {
        _catchHandlers[typeof(TException)] = handler;
        return this;
    }

    public IWorkflowBuilder Finally(Action<IWorkflowBuilder> configure)
    {
        var finallyBuilder = new WorkflowBuilder();
        configure(finallyBuilder);
        var finallyBody = finallyBuilder.Build();
        var step = new TryCatchStep("Try", tryBody, _catchHandlers, finallyBody.Steps.ToArray());
        return parent.Step(step);
    }

    public IWorkflowBuilder EndTry()
    {
        var step = new TryCatchStep("Try", tryBody, _catchHandlers, null);
        return parent.Step(step);
    }
}

/// <summary>
/// Middleware that enforces a timeout on each step.
/// </summary>
public sealed class TimeoutMiddleware : IWorkflowMiddleware
{
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of <see cref="TimeoutMiddleware"/>.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    public TimeoutMiddleware(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Step '{step.Name}' timed out after {_timeout}.");
        }
    }
}
