namespace WorkflowFramework.Extensions.Integration.Composition;

/// <summary>
/// Reorders out-of-sequence items back into correct order.
/// </summary>
public sealed class ResequencerStep : IStep
{
    private readonly Func<IWorkflowContext, IEnumerable<object>> _itemsSelector;
    private readonly Func<object, long> _sequenceSelector;
    /// <summary>
    /// The property key used to store the resequenced items.
    /// </summary>
    public const string ResultKey = "__ResequencerResult";

    /// <summary>
    /// Initializes a new instance of <see cref="ResequencerStep"/>.
    /// </summary>
    /// <param name="itemsSelector">Function to select items to reorder from context.</param>
    /// <param name="sequenceSelector">Function to extract the sequence number from each item.</param>
    public ResequencerStep(
        Func<IWorkflowContext, IEnumerable<object>> itemsSelector,
        Func<object, long> sequenceSelector)
    {
        _itemsSelector = itemsSelector ?? throw new ArgumentNullException(nameof(itemsSelector));
        _sequenceSelector = sequenceSelector ?? throw new ArgumentNullException(nameof(sequenceSelector));
    }

    /// <inheritdoc />
    public string Name => "Resequencer";

    /// <inheritdoc />
    public Task ExecuteAsync(IWorkflowContext context)
    {
        var items = _itemsSelector(context);
        var resequenced = items.OrderBy(_sequenceSelector).ToList();
        context.Properties[ResultKey] = resequenced;
        return Task.CompletedTask;
    }
}
