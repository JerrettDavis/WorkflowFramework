using PatternKit.Messaging.Reliability;

namespace WorkflowFramework.Extensions.Integration.Endpoint;

/// <summary>
/// Ensures duplicate messages are handled only once by tracking message IDs.
/// Uses PatternKit <see cref="IIdempotencyStore"/> with claim → invoke → complete/fail semantics:
/// a successful processing marks the key Completed (suppressing future duplicates), while a
/// failure marks the key Failed and allows retry on the next attempt.
/// </summary>
public sealed class IdempotentReceiverStep : IStep
{
    private readonly IStep _innerStep;
    private readonly Func<IWorkflowContext, string> _messageIdSelector;
    private readonly IIdempotencyStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="IdempotentReceiverStep"/> with an injected
    /// idempotency store.
    /// </summary>
    /// <param name="innerStep">The step to wrap with idempotency.</param>
    /// <param name="messageIdSelector">Function to extract a unique message ID from context.</param>
    /// <param name="store">The idempotency store to use.</param>
    public IdempotentReceiverStep(IStep innerStep, Func<IWorkflowContext, string> messageIdSelector, IIdempotencyStore store)
    {
        _innerStep = innerStep ?? throw new ArgumentNullException(nameof(innerStep));
        _messageIdSelector = messageIdSelector ?? throw new ArgumentNullException(nameof(messageIdSelector));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Initializes a new instance of <see cref="IdempotentReceiverStep"/> with the default
    /// in-memory idempotency store.
    /// </summary>
    /// <param name="innerStep">The step to wrap with idempotency.</param>
    /// <param name="messageIdSelector">Function to extract a unique message ID from context.</param>
    public IdempotentReceiverStep(IStep innerStep, Func<IWorkflowContext, string> messageIdSelector)
        : this(innerStep, messageIdSelector, new RetryAfterFailureIdempotencyStore())
    {
    }

    /// <inheritdoc />
    public string Name => "IdempotentReceiver";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var messageId = _messageIdSelector(context);

        var claim = await _store.TryClaimAsync(messageId, context.CancellationToken).ConfigureAwait(false);
        if (!claim.Claimed)
            return; // Completed — successful duplicate, silently skip.

        try
        {
            await _innerStep.ExecuteAsync(context).ConfigureAwait(false);
            await _store.MarkCompletedAsync(messageId, null, context.CancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _store.MarkFailedAsync(messageId, null, context.CancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// In-memory idempotency store that resets a Failed key to Processing on the next
    /// TryClaim call, allowing retry after a transient failure.
    /// This is the correct production behaviour: dedupe a Completed attempt, allow retry of a
    /// Failed attempt.
    /// </summary>
    private sealed class RetryAfterFailureIdempotencyStore : IIdempotencyStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

        public ValueTask<IdempotencyClaim> TryClaimAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                if (_entries.TryGetValue(key, out var existing))
                {
                    if (existing.Status == IdempotencyEntryStatus.Failed)
                    {
                        // A prior attempt failed — reset to Processing so this attempt proceeds.
                        _entries[key] = new Entry(IdempotencyEntryStatus.Processing);
                        return new ValueTask<IdempotencyClaim>(IdempotencyClaim.ClaimedKey(key));
                    }

                    // Key is Processing or Completed — do not allow concurrent or duplicate processing.
                    return new ValueTask<IdempotencyClaim>(
                        IdempotencyClaim.Existing(key, existing.Status, existing.Result, null));
                }

                _entries[key] = new Entry(IdempotencyEntryStatus.Processing);
                return new ValueTask<IdempotencyClaim>(IdempotencyClaim.ClaimedKey(key));
            }
        }

        public ValueTask MarkCompletedAsync(string key, object? result = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
                _entries[key] = new Entry(IdempotencyEntryStatus.Completed, result);
            return default;
        }

        public ValueTask MarkFailedAsync(string key, string? reason = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
                _entries[key] = new Entry(IdempotencyEntryStatus.Failed);
            return default;
        }

        private sealed class Entry
        {
            internal Entry(IdempotencyEntryStatus status, object? result = null)
            {
                Status = status;
                Result = result;
            }

            internal IdempotencyEntryStatus Status { get; }
            internal object? Result { get; }
        }
    }
}
