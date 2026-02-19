namespace WorkflowFramework.Internal;

/// <summary>
/// A step that iterates over a collection and executes child steps for each item.
/// </summary>
internal sealed class ForEachStep<TItem>(string name, Func<IWorkflowContext, IEnumerable<TItem>> itemsSelector, IStep[] body)
    : IStep
{
    public string Name { get; } = name;

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var items = itemsSelector(context);
        var index = 0;
        foreach (var item in items)
        {
            context.Properties["ForEach.Current"] = item;
            context.Properties["ForEach.Index"] = index++;
            foreach (var step in body)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (context.IsAborted) return;
                await step.ExecuteAsync(context).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// A step that loops while a condition is true.
/// </summary>
internal sealed class WhileStep(string name, Func<IWorkflowContext, bool> condition, IStep[] body) : IStep
{
    public string Name { get; } = name;

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        while (condition(context))
        {
            foreach (var step in body)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (context.IsAborted) return;
                await step.ExecuteAsync(context).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// A step that executes body then loops while condition is true.
/// </summary>
internal sealed class DoWhileStep(string name, IStep[] body, Func<IWorkflowContext, bool> condition) : IStep
{
    public string Name { get; } = name;

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        do
        {
            foreach (var step in body)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (context.IsAborted) return;
                await step.ExecuteAsync(context).ConfigureAwait(false);
            }
        } while (condition(context));
    }
}

/// <summary>
/// A step that retries a group of steps up to a maximum number of attempts.
/// </summary>
internal sealed class RetryGroupStep(string name, IStep[] body, int maxAttempts) : IStep
{
    public string Name { get; } = name;

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                context.Properties["Retry.Attempt"] = attempt;
                foreach (var step in body)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (context.IsAborted) return;
                    await step.ExecuteAsync(context).ConfigureAwait(false);
                }
                return; // Success
            }
            catch when (attempt < maxAttempts)
            {
                // Retry
            }
        }
    }
}

/// <summary>
/// A step that implements try/catch/finally semantics.
/// </summary>
internal sealed class TryCatchStep(
    string name,
    IStep[] tryBody,
    Dictionary<Type, Func<IWorkflowContext, Exception, Task>> catchHandlers,
    IStep[]? finallyBody
)
    : IStep
{
    public string Name { get; } = name;

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        try
        {
            foreach (var step in tryBody)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (context.IsAborted) return;
                await step.ExecuteAsync(context).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (FindHandler(ex) is { } handler)
        {
            await handler(context, ex).ConfigureAwait(false);
        }
        finally
        {
            if (finallyBody != null)
            {
                foreach (var step in finallyBody)
                {
                    await step.ExecuteAsync(context).ConfigureAwait(false);
                }
            }
        }
    }

    private Func<IWorkflowContext, Exception, Task>? FindHandler(Exception ex)
    {
        var exType = ex.GetType();
        while (exType != null)
        {
            if (catchHandlers.TryGetValue(exType, out var handler))
                return handler;
            exType = exType.BaseType;
        }
        return null;
    }
}

/// <summary>
/// A step that delegates to a sub-workflow.
/// </summary>
internal sealed class SubWorkflowStep(IWorkflow subWorkflow) : IStep
{
    public string Name => $"SubWorkflow({subWorkflow.Name})";

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var result = await subWorkflow.ExecuteAsync(context).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            context.IsAborted = true;
        }
    }
}

/// <summary>
/// A step that introduces a delay.
/// </summary>
internal sealed class DelayStep(TimeSpan delay) : IStep
{
    public string Name => $"Delay({delay})";

    public Task ExecuteAsync(IWorkflowContext context) =>
        Task.Delay(delay, context.CancellationToken);
}

/// <summary>
/// A step that wraps another step with a timeout.
/// </summary>
internal sealed class TimeoutStep(IStep inner, TimeSpan timeout) : IStep
{
    public string Name => $"Timeout({inner.Name}, {timeout})";

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(timeout);

        // Create a new context wrapper that uses the timeout CTS
        var timeoutContext = new TimeoutContextWrapper(context, cts.Token);

        try
        {
            await inner.ExecuteAsync(timeoutContext).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Step '{inner.Name}' timed out after {timeout}.");
        }
    }

    private sealed class TimeoutContextWrapper(IWorkflowContext inner, CancellationToken cancellationToken) : IWorkflowContext
    {
        public string WorkflowId => inner.WorkflowId;
        public string CorrelationId => inner.CorrelationId;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public IDictionary<string, object?> Properties => inner.Properties;
        public string? CurrentStepName { get => inner.CurrentStepName; set => inner.CurrentStepName = value; }
        public int CurrentStepIndex { get => inner.CurrentStepIndex; set => inner.CurrentStepIndex = value; }
        public bool IsAborted { get => inner.IsAborted; set => inner.IsAborted = value; }
        public IList<WorkflowError> Errors => inner.Errors;
    }
}
