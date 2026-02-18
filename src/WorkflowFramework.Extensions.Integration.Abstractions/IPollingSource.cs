namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Polls an external source for new data items.
/// </summary>
/// <typeparam name="T">The type of data items.</typeparam>
public interface IPollingSource<T>
{
    /// <summary>
    /// Polls the source for available items.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The available items, or an empty collection if none.</returns>
    Task<IReadOnlyList<T>> PollAsync(CancellationToken cancellationToken = default);
}
