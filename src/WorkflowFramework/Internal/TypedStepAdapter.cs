namespace WorkflowFramework.Internal;

/// <summary>
/// Adapts a typed <see cref="IStep{TData}"/> to the untyped <see cref="IStep"/> interface.
/// </summary>
internal sealed class TypedStepAdapter<TData>(IStep<TData> inner) : IStep
    where TData : class
{
    private readonly IStep<TData> _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public string Name => _inner.Name;

    public Task ExecuteAsync(IWorkflowContext context) =>
        _inner.ExecuteAsync((IWorkflowContext<TData>)context);
}

/// <summary>
/// Adapts a typed <see cref="ICompensatingStep{TData}"/> to the untyped <see cref="ICompensatingStep"/> interface.
/// </summary>
internal sealed class TypedCompensatingStepAdapter<TData>(ICompensatingStep<TData> inner) : ICompensatingStep
    where TData : class
{
    private readonly ICompensatingStep<TData> _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public string Name => _inner.Name;

    public Task ExecuteAsync(IWorkflowContext context) =>
        _inner.ExecuteAsync((IWorkflowContext<TData>)context);

    public Task CompensateAsync(IWorkflowContext context) =>
        _inner.CompensateAsync((IWorkflowContext<TData>)context);
}
