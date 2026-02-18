namespace WorkflowFramework.Extensions.Integration.Endpoint;

/// <summary>
/// Ensures duplicate messages are handled only once by tracking message IDs.
/// </summary>
public sealed class IdempotentReceiverStep : IStep
{
    private readonly IStep _innerStep;
    private readonly Func<IWorkflowContext, string> _messageIdSelector;
    private readonly HashSet<string> _processedIds = new();
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="IdempotentReceiverStep"/>.
    /// </summary>
    /// <param name="innerStep">The step to wrap with idempotency.</param>
    /// <param name="messageIdSelector">Function to extract a unique message ID from context.</param>
    public IdempotentReceiverStep(IStep innerStep, Func<IWorkflowContext, string> messageIdSelector)
    {
        _innerStep = innerStep ?? throw new ArgumentNullException(nameof(innerStep));
        _messageIdSelector = messageIdSelector ?? throw new ArgumentNullException(nameof(messageIdSelector));
    }

    /// <inheritdoc />
    public string Name => "IdempotentReceiver";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var messageId = _messageIdSelector(context);

        lock (_lock)
        {
            if (!_processedIds.Add(messageId))
                return; // Already processed
        }

        await _innerStep.ExecuteAsync(context).ConfigureAwait(false);
    }
}
