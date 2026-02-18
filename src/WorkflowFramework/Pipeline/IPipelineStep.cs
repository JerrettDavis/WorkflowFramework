namespace WorkflowFramework.Pipeline;

/// <summary>
/// Represents a step with explicit input and output types for pipeline composition.
/// </summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
public interface IPipelineStep<in TInput, TOutput>
{
    /// <summary>
    /// Gets the name of this pipeline step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes this step with the given input.
    /// </summary>
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The output data.</returns>
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}
