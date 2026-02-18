namespace WorkflowFramework.Internal;

/// <summary>
/// Adapts a typed <see cref="IStep{TData}"/> to the untyped <see cref="IStep"/> interface.
/// </summary>
internal sealed class TypedStepAdapter<TData> : IStep where TData : class
{
    private readonly IStep<TData> _inner;

    public TypedStepAdapter(IStep<TData> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string Name => _inner.Name;

    public Task ExecuteAsync(IWorkflowContext context) =>
        _inner.ExecuteAsync((IWorkflowContext<TData>)context);
}

/// <summary>
/// Adapts a typed <see cref="ICompensatingStep{TData}"/> to the untyped <see cref="ICompensatingStep"/> interface.
/// </summary>
internal sealed class TypedCompensatingStepAdapter<TData> : ICompensatingStep where TData : class
{
    private readonly ICompensatingStep<TData> _inner;

    public TypedCompensatingStepAdapter(ICompensatingStep<TData> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string Name => _inner.Name;

    public Task ExecuteAsync(IWorkflowContext context) =>
        _inner.ExecuteAsync((IWorkflowContext<TData>)context);

    public Task CompensateAsync(IWorkflowContext context) =>
        _inner.CompensateAsync((IWorkflowContext<TData>)context);
}
