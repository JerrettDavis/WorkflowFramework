using PatternKit.Messaging.Reliability;

namespace WorkflowFramework.Extensions.Integration.Endpoint;

/// <summary>
/// Writes messages to an outbox atomically with business data.
/// Internally delegates to <see cref="IOutboxStore{TPayload}">PatternKit IOutboxStore&lt;object&gt;</see>.
/// </summary>
public sealed class TransactionalOutboxStep : IStep
{
    private readonly IOutboxStore<object> _outboxStore;
    private readonly Func<IWorkflowContext, object> _messageSelector;

    /// <summary>
    /// The property key used to store the outbox message ID.
    /// </summary>
    public const string OutboxIdKey = "__OutboxMessageId";

    /// <summary>
    /// Initializes a new instance of <see cref="TransactionalOutboxStep"/> consuming
    /// <see cref="IOutboxStore{TPayload}">PatternKit IOutboxStore&lt;object&gt;</see>.
    /// </summary>
    /// <param name="outboxStore">The PatternKit typed outbox store.</param>
    /// <param name="messageSelector">Function to extract the message payload to outbox from context.</param>
    public TransactionalOutboxStep(IOutboxStore<object> outboxStore, Func<IWorkflowContext, object> messageSelector)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _messageSelector = messageSelector ?? throw new ArgumentNullException(nameof(messageSelector));
    }

    /// <inheritdoc />
    public string Name => "TransactionalOutbox";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var payload = _messageSelector(context);
        var record = await _outboxStore.EnqueueObjectAsync(payload, headers: null, context.CancellationToken)
                                       .ConfigureAwait(false);
        context.Properties[OutboxIdKey] = record.Id;
    }
}
