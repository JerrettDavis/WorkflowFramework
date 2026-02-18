namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Transactional outbox store for reliable message publishing.
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Saves a message to the outbox atomically with business data.
    /// </summary>
    /// <param name="message">The message to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outbox message identifier.</returns>
    Task<string> SaveAsync(object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pending (unsent) messages from the outbox.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pending outbox messages.</returns>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as sent/published.
    /// </summary>
    /// <param name="messageId">The outbox message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsSentAsync(string messageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a message stored in the transactional outbox.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// Gets or sets the unique message identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message payload.
    /// </summary>
    public object Payload { get; set; } = null!;

    /// <summary>
    /// Gets or sets the time the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets whether the message has been sent.
    /// </summary>
    public bool IsSent { get; set; }
}
