using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Endpoint;

/// <summary>
/// Writes messages to an outbox table atomically with business data.
/// </summary>
public sealed class TransactionalOutboxStep : IStep
{
    private readonly IOutboxStore _outboxStore;
    private readonly Func<IWorkflowContext, object> _messageSelector;
    /// <summary>
    /// The property key used to store the outbox message ID.
    /// </summary>
    public const string OutboxIdKey = "__OutboxMessageId";

    /// <summary>
    /// Initializes a new instance of <see cref="TransactionalOutboxStep"/>.
    /// </summary>
    /// <param name="outboxStore">The outbox store.</param>
    /// <param name="messageSelector">Function to extract the message to outbox from context.</param>
    public TransactionalOutboxStep(IOutboxStore outboxStore, Func<IWorkflowContext, object> messageSelector)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _messageSelector = messageSelector ?? throw new ArgumentNullException(nameof(messageSelector));
    }

    /// <inheritdoc />
    public string Name => "TransactionalOutbox";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var message = _messageSelector(context);
        var id = await _outboxStore.SaveAsync(message, context.CancellationToken).ConfigureAwait(false);
        context.Properties[OutboxIdKey] = id;
    }
}
