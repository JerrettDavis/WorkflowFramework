namespace WorkflowFramework.Tests.TinyBDD.Testing;

/// <summary>
/// Inline step that delegates execution to a supplied lambda.
/// Used in test scenarios where a dedicated step class would add noise.
/// </summary>
internal sealed class LambdaStep : IStep
{
    private readonly Func<IWorkflowContext, Task> _action;

    public LambdaStep(string name, Func<IWorkflowContext, Task> action)
    {
        Name = name;
        _action = action;
    }

    public string Name { get; }
    public Task ExecuteAsync(IWorkflowContext context) => _action(context);
}

/// <summary>
/// Typed inline step that delegates execution to a supplied lambda.
/// </summary>
internal sealed class LambdaStep<TData> : IStep<TData> where TData : class
{
    private readonly Func<IWorkflowContext<TData>, Task> _action;

    public LambdaStep(string name, Func<IWorkflowContext<TData>, Task> action)
    {
        Name = name;
        _action = action;
    }

    public string Name { get; }
    public Task ExecuteAsync(IWorkflowContext<TData> context) => _action(context);
}
