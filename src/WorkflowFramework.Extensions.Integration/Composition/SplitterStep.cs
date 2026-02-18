namespace WorkflowFramework.Extensions.Integration.Composition;

/// <summary>
/// Splits a collection into individual items for parallel or sequential processing.
/// </summary>
public sealed class SplitterStep : IStep
{
    private readonly Func<IWorkflowContext, IEnumerable<object>> _splitter;
    private readonly IStep _itemProcessor;
    private readonly bool _parallel;
    /// <summary>
    /// The property key used to store the current item being processed.
    /// </summary>
    public const string CurrentItemKey = "__SplitterCurrentItem";
    /// <summary>
    /// The property key used to store processed results.
    /// </summary>
    public const string ResultsKey = "__SplitterResults";

    /// <summary>
    /// Initializes a new instance of <see cref="SplitterStep"/>.
    /// </summary>
    /// <param name="splitter">Function to split context data into individual items.</param>
    /// <param name="itemProcessor">Step to process each individual item.</param>
    /// <param name="parallel">Whether to process items in parallel.</param>
    public SplitterStep(
        Func<IWorkflowContext, IEnumerable<object>> splitter,
        IStep itemProcessor,
        bool parallel = false)
    {
        _splitter = splitter ?? throw new ArgumentNullException(nameof(splitter));
        _itemProcessor = itemProcessor ?? throw new ArgumentNullException(nameof(itemProcessor));
        _parallel = parallel;
    }

    /// <inheritdoc />
    public string Name => "Splitter";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var items = _splitter(context).ToList();
        var results = new List<object?>();

        if (_parallel)
        {
            var tasks = items.Select(async item =>
            {
                context.Properties[CurrentItemKey] = item;
                await _itemProcessor.ExecuteAsync(context).ConfigureAwait(false);
                return context.Properties.TryGetValue($"__ProcessedItem", out var result) ? result : item;
            });
            results.AddRange(await Task.WhenAll(tasks).ConfigureAwait(false));
        }
        else
        {
            foreach (var item in items)
            {
                context.Properties[CurrentItemKey] = item;
                await _itemProcessor.ExecuteAsync(context).ConfigureAwait(false);
                var result = context.Properties.TryGetValue("__ProcessedItem", out var r) ? r : item;
                results.Add(result);
            }
        }

        context.Properties[ResultsKey] = results;
    }
}
