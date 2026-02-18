namespace WorkflowFramework.Pipeline;

/// <summary>
/// Static entry point for creating typed pipelines.
/// </summary>
public static class Pipeline
{
    /// <summary>
    /// Creates a new pipeline builder starting with the given input type.
    /// </summary>
    /// <typeparam name="TInput">The initial input type.</typeparam>
    /// <returns>A new pipeline builder.</returns>
    public static IPipelineBuilder<TInput, TInput> Create<TInput>() =>
        new PipelineBuilder<TInput, TInput>((input, _) => Task.FromResult(input));
}

/// <summary>
/// Builder for constructing typed pipelines.
/// </summary>
/// <typeparam name="TIn">The pipeline input type.</typeparam>
/// <typeparam name="TCurrent">The current output type.</typeparam>
public interface IPipelineBuilder<TIn, TCurrent>
{
    /// <summary>
    /// Adds a step to the pipeline.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <typeparam name="TOut">The output type of the step.</typeparam>
    /// <returns>A new builder with the updated output type.</returns>
    IPipelineBuilder<TIn, TOut> Pipe<TStep, TOut>() where TStep : IPipelineStep<TCurrent, TOut>, new();

    /// <summary>
    /// Adds a step instance to the pipeline.
    /// </summary>
    /// <typeparam name="TOut">The output type of the step.</typeparam>
    /// <param name="step">The step instance.</param>
    /// <returns>A new builder with the updated output type.</returns>
    IPipelineBuilder<TIn, TOut> Pipe<TOut>(IPipelineStep<TCurrent, TOut> step);

    /// <summary>
    /// Adds a delegate step to the pipeline.
    /// </summary>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="transform">The transformation function.</param>
    /// <returns>A new builder with the updated output type.</returns>
    IPipelineBuilder<TIn, TOut> Pipe<TOut>(Func<TCurrent, CancellationToken, Task<TOut>> transform);

    /// <summary>
    /// Builds the pipeline into an executable function.
    /// </summary>
    /// <returns>A function that executes the pipeline.</returns>
    Func<TIn, CancellationToken, Task<TCurrent>> Build();
}

internal sealed class PipelineBuilder<TIn, TCurrent> : IPipelineBuilder<TIn, TCurrent>
{
    private readonly Func<TIn, CancellationToken, Task<TCurrent>> _chain;

    internal PipelineBuilder(Func<TIn, CancellationToken, Task<TCurrent>> chain)
    {
        _chain = chain;
    }

    public IPipelineBuilder<TIn, TOut> Pipe<TStep, TOut>() where TStep : IPipelineStep<TCurrent, TOut>, new()
        => Pipe(new TStep());

    public IPipelineBuilder<TIn, TOut> Pipe<TOut>(IPipelineStep<TCurrent, TOut> step)
    {
        if (step == null) throw new ArgumentNullException(nameof(step));
        var prev = _chain;
        return new PipelineBuilder<TIn, TOut>(async (input, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            var current = await prev(input, ct).ConfigureAwait(false);
            return await step.ExecuteAsync(current, ct).ConfigureAwait(false);
        });
    }

    public IPipelineBuilder<TIn, TOut> Pipe<TOut>(Func<TCurrent, CancellationToken, Task<TOut>> transform)
    {
        if (transform == null) throw new ArgumentNullException(nameof(transform));
        var prev = _chain;
        return new PipelineBuilder<TIn, TOut>(async (input, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            var current = await prev(input, ct).ConfigureAwait(false);
            return await transform(current, ct).ConfigureAwait(false);
        });
    }

    public Func<TIn, CancellationToken, Task<TCurrent>> Build()
    {
        return _chain;
    }
}
