using WorkflowFramework.Builder;

namespace WorkflowFramework;

/// <summary>
/// Static entry point for creating workflows using the fluent builder API.
/// </summary>
public static class Workflow
{
    /// <summary>
    /// Creates a new workflow builder.
    /// </summary>
    /// <returns>A new <see cref="IWorkflowBuilder"/>.</returns>
    public static IWorkflowBuilder Create() => new WorkflowBuilder();

    /// <summary>
    /// Creates a new workflow builder with the given name.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <returns>A new <see cref="IWorkflowBuilder"/>.</returns>
    public static IWorkflowBuilder Create(string name) => new WorkflowBuilder().WithName(name);

    /// <summary>
    /// Creates a new strongly-typed workflow builder.
    /// </summary>
    /// <typeparam name="TData">The type of the workflow data.</typeparam>
    /// <returns>A new <see cref="IWorkflowBuilder{TData}"/>.</returns>
    public static IWorkflowBuilder<TData> Create<TData>() where TData : class =>
        new WorkflowBuilder<TData>();

    /// <summary>
    /// Creates a new strongly-typed workflow builder with the given name.
    /// </summary>
    /// <typeparam name="TData">The type of the workflow data.</typeparam>
    /// <param name="name">The workflow name.</param>
    /// <returns>A new <see cref="IWorkflowBuilder{TData}"/>.</returns>
    public static IWorkflowBuilder<TData> Create<TData>(string name) where TData : class =>
        new WorkflowBuilder<TData>().WithName(name);
}
