using WorkflowFramework.Builder;

namespace WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;

/// <summary>
/// Extension methods to add execution history tracking to a workflow builder.
/// </summary>
public static class ExecutionHistoryBuilderExtensions
{
    /// <summary>
    /// Adds execution history tracking to the workflow. Records step-level timing,
    /// status, and property snapshots to the provided store.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="store">The execution history store.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder WithExecutionHistory(this IWorkflowBuilder builder, IExecutionHistoryStore store)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        return builder
            .Use(new ExecutionHistoryMiddleware())
            .WithEvents(new ExecutionHistoryEvents(store));
    }

    /// <summary>
    /// Adds execution history tracking to the workflow using an in-memory store.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="store">When this method returns, contains the in-memory store instance for querying.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder WithExecutionHistory(this IWorkflowBuilder builder, out InMemoryExecutionHistoryStore store)
    {
        store = new InMemoryExecutionHistoryStore();
        return builder.WithExecutionHistory(store);
    }

    /// <summary>
    /// Adds execution history tracking to a typed workflow builder.
    /// </summary>
    /// <typeparam name="TData">The workflow data type.</typeparam>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="store">The execution history store.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder<TData> WithExecutionHistory<TData>(this IWorkflowBuilder<TData> builder, IExecutionHistoryStore store) where TData : class
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        return builder
            .Use(new ExecutionHistoryMiddleware())
            .WithEvents(new ExecutionHistoryEvents(store));
    }
}
