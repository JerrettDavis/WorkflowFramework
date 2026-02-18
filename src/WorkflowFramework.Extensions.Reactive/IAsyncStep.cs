namespace WorkflowFramework.Extensions.Reactive;

/// <summary>
/// A step that produces streaming results via <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
/// <typeparam name="TResult">The type of streamed results.</typeparam>
public interface IAsyncStep<out TResult>
{
    /// <summary>
    /// Gets the name of this step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes this step and streams results.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns>An async enumerable of results.</returns>
    IAsyncEnumerable<TResult> ExecuteStreamingAsync(IWorkflowContext context);
}

/// <summary>
/// Adapts an <see cref="IAsyncStep{TResult}"/> to an <see cref="IStep"/> for use in workflows.
/// Collects all streamed results and stores them in the context properties.
/// </summary>
/// <typeparam name="TResult">The type of streamed results.</typeparam>
public sealed class AsyncStepAdapter<TResult> : IStep
{
    private readonly IAsyncStep<TResult> _inner;
    private readonly string _resultsKey;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncStepAdapter{TResult}"/>.
    /// </summary>
    /// <param name="inner">The async step to adapt.</param>
    /// <param name="resultsKey">The property key to store collected results. Defaults to the step name.</param>
    public AsyncStepAdapter(IAsyncStep<TResult> inner, string? resultsKey = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _resultsKey = resultsKey ?? $"{inner.Name}.Results";
    }

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var results = await _inner.CollectAsync(context, context.CancellationToken).ConfigureAwait(false);
        context.Properties[_resultsKey] = results;
    }
}

/// <summary>
/// Extension methods for reactive workflow features.
/// </summary>
public static class ReactiveExtensions
{
    /// <summary>
    /// Executes an async step and collects all results into a list.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="step">The async step.</param>
    /// <param name="context">The workflow context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All collected results.</returns>
    public static async Task<List<TResult>> CollectAsync<TResult>(
        this IAsyncStep<TResult> step,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TResult>();
        await foreach (var item in step.ExecuteStreamingAsync(context).WithCancellation(cancellationToken))
        {
            results.Add(item);
        }
        return results;
    }

    /// <summary>
    /// Executes an async step and invokes a callback for each result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="step">The async step.</param>
    /// <param name="context">The workflow context.</param>
    /// <param name="onResult">Callback for each result.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task ForEachAsync<TResult>(
        this IAsyncStep<TResult> step,
        IWorkflowContext context,
        Func<TResult, Task> onResult,
        CancellationToken cancellationToken = default)
    {
        await foreach (var item in step.ExecuteStreamingAsync(context).WithCancellation(cancellationToken))
        {
            await onResult(item).ConfigureAwait(false);
        }
    }
}
