namespace WorkflowFramework.Internal;

/// <summary>
/// A step that iterates over a collection and executes child steps for each item.
/// </summary>
internal sealed class ForEachStep<TItem> : IStep
{
    private readonly Func<IWorkflowContext, IEnumerable<TItem>> _itemsSelector;
    private readonly IStep[] _body;

    public ForEachStep(string name, Func<IWorkflowContext, IEnumerable<TItem>> itemsSelector, IStep[] body)
    {
        Name = name;
        _itemsSelector = itemsSelector;
        _body = body;
    }

    public string Name { get; }

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var items = _itemsSelector(context);
        var index = 0;
        foreach (var item in items)
        {
            context.Properties["ForEach.Current"] = item;
            context.Properties["ForEach.Index"] = index++;
            foreach (var step in _body)
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
internal sealed class WhileStep : IStep
{
    private readonly Func<IWorkflowContext, bool> _condition;
    private readonly IStep[] _body;

    public WhileStep(string name, Func<IWorkflowContext, bool> condition, IStep[] body)
    {
        Name = name;
        _condition = condition;
        _body = body;
    }

    public string Name { get; }

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        while (_condition(context))
        {
            foreach (var step in _body)
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
internal sealed class DoWhileStep : IStep
{
    private readonly Func<IWorkflowContext, bool> _condition;
    private readonly IStep[] _body;

    public DoWhileStep(string name, IStep[] body, Func<IWorkflowContext, bool> condition)
    {
        Name = name;
        _condition = condition;
        _body = body;
    }

    public string Name { get; }

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        do
        {
            foreach (var step in _body)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (context.IsAborted) return;
                await step.ExecuteAsync(context).ConfigureAwait(false);
            }
        } while (_condition(context));
    }
}

/// <summary>
/// A step that retries a group of steps up to a maximum number of attempts.
/// </summary>
internal sealed class RetryGroupStep : IStep
{
    private readonly IStep[] _body;
    private readonly int _maxAttempts;

    public RetryGroupStep(string name, IStep[] body, int maxAttempts)
    {
        Name = name;
        _body = body;
        _maxAttempts = maxAttempts;
    }

    public string Name { get; }

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                context.Properties["Retry.Attempt"] = attempt;
                foreach (var step in _body)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (context.IsAborted) return;
                    await step.ExecuteAsync(context).ConfigureAwait(false);
                }
                return; // Success
            }
            catch when (attempt < _maxAttempts)
            {
                // Retry
            }
        }
    }
}

/// <summary>
/// A step that implements try/catch/finally semantics.
/// </summary>
internal sealed class TryCatchStep : IStep
{
    private readonly IStep[] _tryBody;
    private readonly Dictionary<Type, Func<IWorkflowContext, Exception, Task>> _catchHandlers;
    private readonly IStep[]? _finallyBody;

    public TryCatchStep(
        string name,
        IStep[] tryBody,
        Dictionary<Type, Func<IWorkflowContext, Exception, Task>> catchHandlers,
        IStep[]? finallyBody)
    {
        Name = name;
        _tryBody = tryBody;
        _catchHandlers = catchHandlers;
        _finallyBody = finallyBody;
    }

    public string Name { get; }

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        try
        {
            foreach (var step in _tryBody)
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
            if (_finallyBody != null)
            {
                foreach (var step in _finallyBody)
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
            if (_catchHandlers.TryGetValue(exType, out var handler))
                return handler;
            exType = exType.BaseType;
        }
        return null;
    }
}

/// <summary>
/// A step that delegates to a sub-workflow.
/// </summary>
internal sealed class SubWorkflowStep : IStep
{
    private readonly IWorkflow _subWorkflow;

    public SubWorkflowStep(IWorkflow subWorkflow)
    {
        _subWorkflow = subWorkflow;
    }

    public string Name => $"SubWorkflow({_subWorkflow.Name})";

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var result = await _subWorkflow.ExecuteAsync(context).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            context.IsAborted = true;
        }
    }
}

/// <summary>
/// A step that introduces a delay.
/// </summary>
internal sealed class DelayStep : IStep
{
    private readonly TimeSpan _delay;

    public DelayStep(TimeSpan delay)
    {
        _delay = delay;
    }

    public string Name => $"Delay({_delay})";

    public Task ExecuteAsync(IWorkflowContext context) =>
        Task.Delay(_delay, context.CancellationToken);
}

/// <summary>
/// A step that wraps another step with a timeout.
/// </summary>
internal sealed class TimeoutStep : IStep
{
    private readonly IStep _inner;
    private readonly TimeSpan _timeout;

    public TimeoutStep(IStep inner, TimeSpan timeout)
    {
        _inner = inner;
        _timeout = timeout;
    }

    public string Name => $"Timeout({_inner.Name}, {_timeout})";

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(_timeout);

        // Create a new context wrapper that uses the timeout CTS
        var timeoutContext = new TimeoutContextWrapper(context, cts.Token);

        try
        {
            await _inner.ExecuteAsync(timeoutContext).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Step '{_inner.Name}' timed out after {_timeout}.");
        }
    }

    private sealed class TimeoutContextWrapper : IWorkflowContext
    {
        private readonly IWorkflowContext _inner;

        public TimeoutContextWrapper(IWorkflowContext inner, CancellationToken cancellationToken)
        {
            _inner = inner;
            CancellationToken = cancellationToken;
        }

        public string WorkflowId => _inner.WorkflowId;
        public string CorrelationId => _inner.CorrelationId;
        public CancellationToken CancellationToken { get; }
        public IDictionary<string, object?> Properties => _inner.Properties;
        public string? CurrentStepName { get => _inner.CurrentStepName; set => _inner.CurrentStepName = value; }
        public int CurrentStepIndex { get => _inner.CurrentStepIndex; set => _inner.CurrentStepIndex = value; }
        public bool IsAborted { get => _inner.IsAborted; set => _inner.IsAborted = value; }
        public IList<WorkflowError> Errors => _inner.Errors;
    }
}
