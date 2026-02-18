namespace WorkflowFramework.Internal;

/// <summary>
/// A step backed by a delegate.
/// </summary>
internal sealed class DelegateStep : IStep
{
    private readonly Func<IWorkflowContext, Task> _action;

    public DelegateStep(string name, Func<IWorkflowContext, Task> action)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public string Name { get; }

    public Task ExecuteAsync(IWorkflowContext context) => _action(context);
}

/// <summary>
/// A typed step backed by a delegate.
/// </summary>
internal sealed class DelegateStep<TData> : IStep<TData> where TData : class
{
    private readonly Func<IWorkflowContext<TData>, Task> _action;

    public DelegateStep(string name, Func<IWorkflowContext<TData>, Task> action)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public string Name { get; }

    public Task ExecuteAsync(IWorkflowContext<TData> context) => _action(context);
}
