namespace WorkflowFramework.Internal;

/// <summary>
/// A step backed by a delegate.
/// </summary>
internal sealed class DelegateStep(string name, Func<IWorkflowContext, Task> action) : IStep
{
    private readonly Func<IWorkflowContext, Task> _action = action ?? throw new ArgumentNullException(nameof(action));

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public Task ExecuteAsync(IWorkflowContext context) => _action(context);
}

/// <summary>
/// A typed step backed by a delegate.
/// </summary>
internal sealed class DelegateStep<TData>(string name, Func<IWorkflowContext<TData>, Task> action) : IStep<TData>
    where TData : class
{
    private readonly Func<IWorkflowContext<TData>, Task> _action = action ?? throw new ArgumentNullException(nameof(action));

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public Task ExecuteAsync(IWorkflowContext<TData> context) => _action(context);
}
