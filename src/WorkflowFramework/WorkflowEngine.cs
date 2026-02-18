namespace WorkflowFramework;

/// <summary>
/// The core workflow execution engine.
/// </summary>
public sealed class WorkflowEngine : IWorkflow
{
    private readonly IStep[] _steps;
    private readonly IWorkflowMiddleware[] _middleware;
    private readonly IWorkflowEvents[] _events;
    private readonly bool _enableCompensation;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowEngine"/>.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <param name="steps">The steps to execute.</param>
    /// <param name="middleware">The middleware pipeline.</param>
    /// <param name="events">The event handlers.</param>
    /// <param name="enableCompensation">Whether saga compensation is enabled.</param>
    public WorkflowEngine(
        string name,
        IStep[] steps,
        IWorkflowMiddleware[] middleware,
        IWorkflowEvents[] events,
        bool enableCompensation)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _middleware = middleware ?? throw new ArgumentNullException(nameof(middleware));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _enableCompensation = enableCompensation;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<IStep> Steps => _steps;

    /// <inheritdoc />
    public async Task<WorkflowResult> ExecuteAsync(IWorkflowContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        await RaiseEventAsync(e => e.OnWorkflowStartedAsync(context)).ConfigureAwait(false);

        var completedSteps = new List<IStep>();

        try
        {
            for (var i = 0; i < _steps.Length; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (context.IsAborted)
                    return new WorkflowResult(WorkflowStatus.Aborted, context);

                var step = _steps[i];
                context.CurrentStepIndex = i;
                context.CurrentStepName = step.Name;

                await RaiseEventAsync(e => e.OnStepStartedAsync(context, step)).ConfigureAwait(false);

                try
                {
                    await ExecuteWithMiddlewareAsync(context, step).ConfigureAwait(false);
                    completedSteps.Add(step);
                    await RaiseEventAsync(e => e.OnStepCompletedAsync(context, step)).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    context.Errors.Add(new WorkflowError(step.Name, ex, DateTimeOffset.UtcNow));
                    await RaiseEventAsync(e => e.OnStepFailedAsync(context, step, ex)).ConfigureAwait(false);

                    if (_enableCompensation)
                    {
                        await CompensateAsync(context, completedSteps).ConfigureAwait(false);
                        await RaiseEventAsync(e => e.OnWorkflowFailedAsync(context, ex)).ConfigureAwait(false);
                        return new WorkflowResult(WorkflowStatus.Compensated, context);
                    }

                    await RaiseEventAsync(e => e.OnWorkflowFailedAsync(context, ex)).ConfigureAwait(false);
                    return new WorkflowResult(WorkflowStatus.Faulted, context);
                }
            }

            await RaiseEventAsync(e => e.OnWorkflowCompletedAsync(context)).ConfigureAwait(false);
            return new WorkflowResult(WorkflowStatus.Completed, context);
        }
        catch (OperationCanceledException)
        {
            return new WorkflowResult(WorkflowStatus.Aborted, context);
        }
    }

    private async Task ExecuteWithMiddlewareAsync(IWorkflowContext context, IStep step)
    {
        if (_middleware.Length == 0)
        {
            await step.ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        // Build middleware chain
        StepDelegate current = ctx => step.ExecuteAsync(ctx);

        for (var i = _middleware.Length - 1; i >= 0; i--)
        {
            var middleware = _middleware[i];
            var next = current;
            current = ctx => middleware.InvokeAsync(ctx, step, next);
        }

        await current(context).ConfigureAwait(false);
    }

    private static async Task CompensateAsync(IWorkflowContext context, List<IStep> completedSteps)
    {
        for (var i = completedSteps.Count - 1; i >= 0; i--)
        {
            if (completedSteps[i] is ICompensatingStep compensating)
            {
                try
                {
                    await compensating.CompensateAsync(context).ConfigureAwait(false);
                }
                catch
                {
                    // Swallow compensation errors to ensure all steps get a chance to compensate
                }
            }
        }
    }

    private async Task RaiseEventAsync(Func<IWorkflowEvents, Task> action)
    {
        foreach (var handler in _events)
        {
            await action(handler).ConfigureAwait(false);
        }
    }
}
