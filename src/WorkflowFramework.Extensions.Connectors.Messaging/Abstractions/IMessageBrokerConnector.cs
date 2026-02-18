using WorkflowFramework.Extensions.Connectors.Abstractions;

namespace WorkflowFramework.Extensions.Connectors.Messaging.Abstractions;

/// <summary>
/// Common interface for all message broker connectors.
/// </summary>
public interface IMessageBrokerConnector : IConnector
{
    /// <summary>
    /// Publishes a message to the broker.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to messages from the broker.
    /// </summary>
    /// <param name="handler">The message handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SubscribeAsync(Func<BrokerMessage, Task> handler, CancellationToken cancellationToken = default);
}

/// <summary>
/// A message to send/receive via a message broker.
/// </summary>
public sealed class BrokerMessage
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content type (e.g., "application/json").
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Gets or sets the message headers/properties.
    /// </summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the routing key/topic/subject.
    /// </summary>
    public string? RoutingKey { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public string? CorrelationId { get; set; }
}
