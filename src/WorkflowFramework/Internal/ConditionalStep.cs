namespace WorkflowFramework.Internal;

/// <summary>
/// A step that conditionally executes one of two branches.
/// </summary>
internal sealed class ConditionalStep(
    Func<IWorkflowContext, bool> condition,
    IStep thenStep,
    IStep? elseStep
)
    : IStep
{
    private readonly Func<IWorkflowContext, bool> _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    private readonly IStep _thenStep = thenStep ?? throw new ArgumentNullException(nameof(thenStep));

    public string Name => $"If({_thenStep.Name}" + (elseStep != null ? $"/{elseStep.Name}" : "") + ")";

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        if (_condition(context))
        {
            await _thenStep.ExecuteAsync(context).ConfigureAwait(false);
        }
        else if (elseStep != null)
        {
            await elseStep.ExecuteAsync(context).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// A typed step that conditionally executes one of two branches.
/// </summary>
internal sealed class ConditionalStep<TData>(
    Func<IWorkflowContext<TData>, bool> condition,
    IStep<TData> thenStep,
    IStep<TData>? elseStep
)
    : IStep
    where TData : class
{
    private readonly Func<IWorkflowContext<TData>, bool> _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    private readonly IStep<TData> _thenStep = thenStep ?? throw new ArgumentNullException(nameof(thenStep));

    public string Name => $"If({_thenStep.Name}" + (elseStep != null ? $"/{elseStep.Name}" : "") + ")";

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var typedContext = (IWorkflowContext<TData>)context;
        if (_condition(typedContext))
        {
            await _thenStep.ExecuteAsync(typedContext).ConfigureAwait(false);
        }
        else if (elseStep != null)
        {
            await elseStep.ExecuteAsync(typedContext).ConfigureAwait(false);
        }
    }
}
