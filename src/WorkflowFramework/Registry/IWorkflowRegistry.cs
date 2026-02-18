namespace WorkflowFramework.Registry;

/// <summary>
/// Registry for storing and resolving workflows by name.
/// </summary>
public interface IWorkflowRegistry
{
    /// <summary>
    /// Registers a workflow factory by name.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <param name="factory">A factory that creates the workflow.</param>
    void Register(string name, Func<IWorkflow> factory);

    /// <summary>
    /// Registers a typed workflow factory by name.
    /// </summary>
    /// <typeparam name="TData">The workflow data type.</typeparam>
    /// <param name="name">The workflow name.</param>
    /// <param name="factory">A factory that creates the workflow.</param>
    void Register<TData>(string name, Func<IWorkflow<TData>> factory) where TData : class;

    /// <summary>
    /// Resolves a workflow by name.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <returns>The workflow instance.</returns>
    IWorkflow Resolve(string name);

    /// <summary>
    /// Resolves a typed workflow by name.
    /// </summary>
    /// <typeparam name="TData">The workflow data type.</typeparam>
    /// <param name="name">The workflow name.</param>
    /// <returns>The typed workflow instance.</returns>
    IWorkflow<TData> Resolve<TData>(string name) where TData : class;

    /// <summary>
    /// Gets all registered workflow names.
    /// </summary>
    IReadOnlyCollection<string> Names { get; }
}

/// <summary>
/// High-level API for running workflows by name.
/// </summary>
public interface IWorkflowRunner
{
    /// <summary>
    /// Runs a workflow by name with the given context.
    /// </summary>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="context">The workflow context.</param>
    /// <returns>The workflow result.</returns>
    Task<WorkflowResult> RunAsync(string workflowName, IWorkflowContext context);

    /// <summary>
    /// Runs a typed workflow by name with the given data.
    /// </summary>
    /// <typeparam name="TData">The workflow data type.</typeparam>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="data">The workflow data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed workflow result.</returns>
    Task<WorkflowResult<TData>> RunAsync<TData>(string workflowName, TData data, CancellationToken cancellationToken = default) where TData : class;
}
