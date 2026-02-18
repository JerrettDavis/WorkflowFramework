namespace WorkflowFramework.Registry;

/// <summary>
/// Default implementation of <see cref="IWorkflowRegistry"/>.
/// </summary>
public sealed class WorkflowRegistry : IWorkflowRegistry
{
    private readonly Dictionary<string, Func<IWorkflow>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _typedFactories = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(string name, Func<IWorkflow> factory)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _factories[name] = factory;
    }

    /// <inheritdoc />
    public void Register<TData>(string name, Func<IWorkflow<TData>> factory) where TData : class
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        var key = $"{name}::{typeof(TData).FullName}";
        _typedFactories[key] = factory;
    }

    /// <inheritdoc />
    public IWorkflow Resolve(string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (_factories.TryGetValue(name, out var factory))
            return factory();
        throw new KeyNotFoundException($"No workflow registered with name '{name}'.");
    }

    /// <inheritdoc />
    public IWorkflow<TData> Resolve<TData>(string name) where TData : class
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        var key = $"{name}::{typeof(TData).FullName}";
        if (_typedFactories.TryGetValue(key, out var factory))
            return ((Func<IWorkflow<TData>>)factory)();
        throw new KeyNotFoundException($"No typed workflow registered with name '{name}' for type '{typeof(TData).Name}'.");
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Names => _factories.Keys;
}

/// <summary>
/// Default implementation of <see cref="IWorkflowRunner"/>.
/// </summary>
public sealed class WorkflowRunner : IWorkflowRunner
{
    private readonly IWorkflowRegistry _registry;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowRunner"/>.
    /// </summary>
    /// <param name="registry">The workflow registry.</param>
    public WorkflowRunner(IWorkflowRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc />
    public Task<WorkflowResult> RunAsync(string workflowName, IWorkflowContext context)
    {
        var workflow = _registry.Resolve(workflowName);
        return workflow.ExecuteAsync(context);
    }

    /// <inheritdoc />
    public Task<WorkflowResult<TData>> RunAsync<TData>(string workflowName, TData data, CancellationToken cancellationToken = default) where TData : class
    {
        var workflow = _registry.Resolve<TData>(workflowName);
        var context = new WorkflowContext<TData>(data, cancellationToken);
        return workflow.ExecuteAsync(context);
    }
}
