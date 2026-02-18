namespace WorkflowFramework.Builder;

/// <summary>
/// Builder for parallel step execution.
/// </summary>
public interface IParallelBuilder
{
    /// <summary>
    /// Adds a step to the parallel group.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    IParallelBuilder Step<TStep>() where TStep : IStep, new();

    /// <summary>
    /// Adds a step instance to the parallel group.
    /// </summary>
    /// <param name="step">The step instance.</param>
    /// <returns>This builder for chaining.</returns>
    IParallelBuilder Step(IStep step);
}

/// <summary>
/// Builder for typed parallel step execution.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IParallelBuilder<TData> where TData : class
{
    /// <summary>
    /// Adds a step to the parallel group.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <returns>This builder for chaining.</returns>
    IParallelBuilder<TData> Step<TStep>() where TStep : IStep<TData>, new();

    /// <summary>
    /// Adds a step instance to the parallel group.
    /// </summary>
    /// <param name="step">The step instance.</param>
    /// <returns>This builder for chaining.</returns>
    IParallelBuilder<TData> Step(IStep<TData> step);
}
