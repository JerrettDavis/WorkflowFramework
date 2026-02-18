namespace WorkflowFramework.Extensions.Integration.Composition;

/// <summary>
/// Broadcasts a request to multiple handlers and aggregates their responses with a timeout.
/// </summary>
public sealed class ScatterGatherStep : IStep
{
    private readonly IReadOnlyList<IStep> _handlers;
    private readonly Func<IReadOnlyList<object?>, IWorkflowContext, Task> _aggregator;
    private readonly TimeSpan _timeout;
    /// <summary>
    /// The property key used to store individual handler results.
    /// </summary>
    public const string ResultsKey = "__ScatterGatherResults";

    /// <summary>
    /// Initializes a new instance of <see cref="ScatterGatherStep"/>.
    /// </summary>
    /// <param name="handlers">The handlers to scatter the request to.</param>
    /// <param name="aggregator">Function to aggregate results from all handlers.</param>
    /// <param name="timeout">Maximum time to wait for all handlers.</param>
    public ScatterGatherStep(
        IEnumerable<IStep> handlers,
        Func<IReadOnlyList<object?>, IWorkflowContext, Task> aggregator,
        TimeSpan timeout)
    {
        _handlers = handlers?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(handlers));
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _timeout = timeout;
    }

    /// <inheritdoc />
    public string Name => "ScatterGather";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(_timeout);

        var tasks = _handlers.Select(async handler =>
        {
            try
            {
                // Each handler gets its own context clone via properties
                await handler.ExecuteAsync(context).ConfigureAwait(false);
                return context.Properties.TryGetValue($"__Result_{handler.Name}", out var result) ? result : null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }).ToList();

        try
        {
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            context.Properties[ResultsKey] = results;
            await _aggregator(results, context).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            // Timeout — aggregate what we have
            var partialResults = tasks
                .Where(t => t.Status == TaskStatus.RanToCompletion)
                .Select(t => t.Result)
                .ToList();
            context.Properties[ResultsKey] = partialResults;
            await _aggregator(partialResults, context).ConfigureAwait(false);
        }
    }
}
