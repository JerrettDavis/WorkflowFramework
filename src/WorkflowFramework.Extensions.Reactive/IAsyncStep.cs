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
