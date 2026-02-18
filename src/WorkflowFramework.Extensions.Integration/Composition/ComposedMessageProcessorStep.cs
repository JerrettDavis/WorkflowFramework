namespace WorkflowFramework.Extensions.Integration.Composition;

/// <summary>
/// Combines splitter → process each → aggregator into a single step.
/// </summary>
public sealed class ComposedMessageProcessorStep : IStep
{
    private readonly Func<IWorkflowContext, IEnumerable<object>> _splitter;
    private readonly IStep _processor;
    private readonly Func<IReadOnlyList<object>, IWorkflowContext, Task> _aggregator;
    /// <summary>
    /// The property key used to store the final aggregated result.
    /// </summary>
    public const string ResultKey = "__ComposedResult";

    /// <summary>
    /// Initializes a new instance of <see cref="ComposedMessageProcessorStep"/>.
    /// </summary>
    /// <param name="splitter">Function to split context data into individual items.</param>
    /// <param name="processor">Step to process each item.</param>
    /// <param name="aggregator">Function to aggregate processed results.</param>
    public ComposedMessageProcessorStep(
        Func<IWorkflowContext, IEnumerable<object>> splitter,
        IStep processor,
        Func<IReadOnlyList<object>, IWorkflowContext, Task> aggregator)
    {
        _splitter = splitter ?? throw new ArgumentNullException(nameof(splitter));
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
    }

    /// <inheritdoc />
    public string Name => "ComposedMessageProcessor";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var items = _splitter(context).ToList();
        var results = new List<object>();

        foreach (var item in items)
        {
            context.Properties[SplitterStep.CurrentItemKey] = item;
            await _processor.ExecuteAsync(context).ConfigureAwait(false);
            var result = context.Properties.TryGetValue("__ProcessedItem", out var r) ? r! : item;
            results.Add(result);
        }

        await _aggregator(results, context).ConfigureAwait(false);
    }
}
