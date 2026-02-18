namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Strategy for aggregating multiple items into a single result.
/// </summary>
/// <typeparam name="T">The type of items being aggregated.</typeparam>
public interface IAggregationStrategy<T>
{
    /// <summary>
    /// Aggregates a collection of items into a single result.
    /// </summary>
    /// <param name="items">The items to aggregate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated result.</returns>
    Task<T> AggregateAsync(IReadOnlyList<T> items, CancellationToken cancellationToken = default);
}
