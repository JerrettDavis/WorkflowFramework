namespace WorkflowFramework.Extensions.Integration.Composition;

/// <summary>
/// Options for configuring aggregation completion conditions.
/// </summary>
public sealed class AggregatorOptions
{
    internal Func<IReadOnlyList<object>, bool>? CompletionPredicate { get; set; }
    internal int? CompletionCount { get; set; }
    internal TimeSpan? CompletionTimeout { get; set; }

    /// <summary>
    /// Sets a predicate-based completion condition.
    /// </summary>
    /// <param name="predicate">Predicate evaluated against collected items.</param>
    /// <returns>This options instance for chaining.</returns>
    public AggregatorOptions CompleteWhen(Func<IReadOnlyList<object>, bool> predicate)
    {
        CompletionPredicate = predicate;
        return this;
    }

    /// <summary>
    /// Sets a count-based completion condition.
    /// </summary>
    /// <param name="count">The number of items to collect before completing.</param>
    /// <returns>This options instance for chaining.</returns>
    public AggregatorOptions CompleteAfterCount(int count)
    {
        CompletionCount = count;
        return this;
    }

    /// <summary>
    /// Sets a timeout-based completion condition.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for items.</param>
    /// <returns>This options instance for chaining.</returns>
    public AggregatorOptions Timeout(TimeSpan timeout)
    {
        CompletionTimeout = timeout;
        return this;
    }
}

/// <summary>
/// Collects related messages and combines them into a single output.
/// Supports count, timeout, and predicate completion conditions.
/// </summary>
public sealed class AggregatorStep : IStep
{
    private readonly Func<IWorkflowContext, IEnumerable<object>> _itemsSelector;
    private readonly Func<IReadOnlyList<object>, IWorkflowContext, Task> _aggregateAction;
    private readonly AggregatorOptions _options;
    /// <summary>
    /// The property key used to store the aggregated result.
    /// </summary>
    public const string ResultKey = "__AggregatorResult";

    /// <summary>
    /// Initializes a new instance of <see cref="AggregatorStep"/>.
    /// </summary>
    /// <param name="itemsSelector">Function to select items to aggregate from context.</param>
    /// <param name="aggregateAction">Action to perform aggregation and store results.</param>
    /// <param name="options">Aggregation options (completion conditions).</param>
    public AggregatorStep(
        Func<IWorkflowContext, IEnumerable<object>> itemsSelector,
        Func<IReadOnlyList<object>, IWorkflowContext, Task> aggregateAction,
        AggregatorOptions? options = null)
    {
        _itemsSelector = itemsSelector ?? throw new ArgumentNullException(nameof(itemsSelector));
        _aggregateAction = aggregateAction ?? throw new ArgumentNullException(nameof(aggregateAction));
        _options = options ?? new AggregatorOptions();
    }

    /// <inheritdoc />
    public string Name => "Aggregator";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var allItems = _itemsSelector(context).ToList();
        List<object> collectedItems;

        if (_options.CompletionCount.HasValue)
        {
            collectedItems = allItems.Take(_options.CompletionCount.Value).ToList();
        }
        else if (_options.CompletionPredicate != null)
        {
            collectedItems = new List<object>();
            foreach (var item in allItems)
            {
                collectedItems.Add(item);
                if (_options.CompletionPredicate(collectedItems))
                    break;
            }
        }
        else
        {
            collectedItems = allItems;
        }

        await _aggregateAction(collectedItems, context).ConfigureAwait(false);
    }
}
