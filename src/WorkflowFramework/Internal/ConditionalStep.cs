namespace WorkflowFramework.Internal;

/// <summary>
/// A step that conditionally executes one of two branches.
/// </summary>
internal sealed class ConditionalStep : IStep
{
    private readonly Func<IWorkflowContext, bool> _condition;
    private readonly IStep _thenStep;
    private readonly IStep? _elseStep;

    public ConditionalStep(
        Func<IWorkflowContext, bool> condition,
        IStep thenStep,
        IStep? elseStep)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _thenStep = thenStep ?? throw new ArgumentNullException(nameof(thenStep));
        _elseStep = elseStep;
    }

    public string Name => $"If({_thenStep.Name}" + (_elseStep != null ? $"/{_elseStep.Name}" : "") + ")";

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        if (_condition(context))
        {
            await _thenStep.ExecuteAsync(context).ConfigureAwait(false);
        }
        else if (_elseStep != null)
        {
            await _elseStep.ExecuteAsync(context).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// A typed step that conditionally executes one of two branches.
/// </summary>
internal sealed class ConditionalStep<TData> : IStep where TData : class
{
    private readonly Func<IWorkflowContext<TData>, bool> _condition;
    private readonly IStep<TData> _thenStep;
    private readonly IStep<TData>? _elseStep;

    public ConditionalStep(
        Func<IWorkflowContext<TData>, bool> condition,
        IStep<TData> thenStep,
        IStep<TData>? elseStep)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _thenStep = thenStep ?? throw new ArgumentNullException(nameof(thenStep));
        _elseStep = elseStep;
    }

    public string Name => $"If({_thenStep.Name}" + (_elseStep != null ? $"/{_elseStep.Name}" : "") + ")";

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var typedContext = (IWorkflowContext<TData>)context;
        if (_condition(typedContext))
        {
            await _thenStep.ExecuteAsync(typedContext).ConfigureAwait(false);
        }
        else if (_elseStep != null)
        {
            await _elseStep.ExecuteAsync(typedContext).ConfigureAwait(false);
        }
    }
}
