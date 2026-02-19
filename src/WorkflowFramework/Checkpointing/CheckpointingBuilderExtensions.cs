using WorkflowFramework.Builder;

namespace WorkflowFramework.Checkpointing;

/// <summary>
/// Extension methods for adding checkpointing to a workflow builder.
/// </summary>
public static class CheckpointingBuilderExtensions
{
    /// <summary>
    /// Adds checkpointing middleware that saves a checkpoint after each successful step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="store">The checkpoint store to use.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder WithCheckpointing(this IWorkflowBuilder builder, IWorkflowCheckpointStore store)
    {
        return builder.Use(new CheckpointingMiddleware(store));
    }

    /// <summary>
    /// Adds checkpointing middleware that saves a checkpoint after each successful step.
    /// </summary>
    /// <typeparam name="TData">The workflow data type.</typeparam>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="store">The checkpoint store to use.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder<TData> WithCheckpointing<TData>(this IWorkflowBuilder<TData> builder, IWorkflowCheckpointStore store)
        where TData : class
    {
        return builder.Use(new CheckpointingMiddleware(store));
    }
}
