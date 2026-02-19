namespace WorkflowFramework.Extensions.DataMapping.Batch;

/// <summary>
/// Workflow step that processes collections in configurable batches with optional parallelism.
/// Items are read from the <c>__BatchItems</c> context property.
/// </summary>
public sealed class BatchProcessStep : StepBase
{
    /// <summary>
    /// Property key for the collection of items to batch-process.
    /// </summary>
    public const string BatchItemsKey = "__BatchItems";

    /// <summary>
    /// Property key for the batch processing results.
    /// </summary>
    public const string BatchResultsKey = "__BatchResults";

    private readonly Func<IReadOnlyList<object>, IWorkflowContext, Task> _processBatch;
    private readonly BatchOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="BatchProcessStep"/>.
    /// </summary>
    /// <param name="processBatch">The delegate to process each batch.</param>
    /// <param name="options">Batch processing options.</param>
    public BatchProcessStep(
        Func<IReadOnlyList<object>, IWorkflowContext, Task> processBatch,
        BatchOptions? options = null)
    {
        _processBatch = processBatch ?? throw new ArgumentNullException(nameof(processBatch));
        _options = options ?? new BatchOptions();
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IWorkflowContext context)
    {
        if (!context.Properties.TryGetValue(BatchItemsKey, out var itemsObj) || itemsObj is not IEnumerable<object> items)
            throw new InvalidOperationException($"No items found in context property '{BatchItemsKey}'.");

        var allItems = items.ToList();
        var batches = Batch(allItems, _options.BatchSize);
        var results = new List<object>();

        if (_options.MaxConcurrency > 1)
        {
            using var semaphore = new SemaphoreSlim(_options.MaxConcurrency);
            var tasks = batches.Select(async batch =>
            {
                await semaphore.WaitAsync(context.CancellationToken).ConfigureAwait(false);
                try
                {
                    await _processBatch(batch, context).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            });
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        else
        {
            foreach (var batch in batches)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                await _processBatch(batch, context).ConfigureAwait(false);
            }
        }

        context.Properties[BatchResultsKey] = results;
    }

    private static IEnumerable<IReadOnlyList<object>> Batch(List<object> items, int batchSize)
    {
        for (var i = 0; i < items.Count; i += batchSize)
            yield return items.GetRange(i, Math.Min(batchSize, items.Count - i));
    }
}

/// <summary>
/// Options for batch processing.
/// </summary>
public sealed class BatchOptions
{
    /// <summary>
    /// Gets or sets the number of items per batch. Default is 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum concurrent batches. Default is 1 (sequential).
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;
}

/// <summary>
/// Splits data collections into batches.
/// </summary>
public interface IDataBatcher
{
    /// <summary>
    /// Splits the items into batches of the specified size.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items to batch.</param>
    /// <param name="batchSize">The batch size.</param>
    /// <returns>Batches of items.</returns>
    IEnumerable<IReadOnlyList<T>> Batch<T>(IEnumerable<T> items, int batchSize);
}

/// <summary>
/// Default implementation of <see cref="IDataBatcher"/>.
/// </summary>
public sealed class DataBatcher : IDataBatcher
{
    /// <inheritdoc />
    public IEnumerable<IReadOnlyList<T>> Batch<T>(IEnumerable<T> items, int batchSize)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));

        var batch = new List<T>(batchSize);
        foreach (var item in items)
        {
            batch.Add(item);
            if (batch.Count >= batchSize)
            {
                yield return batch.ToList();
                batch.Clear();
            }
        }
        if (batch.Count > 0)
            yield return batch;
    }
}
