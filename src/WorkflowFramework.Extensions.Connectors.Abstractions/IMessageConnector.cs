namespace WorkflowFramework.Extensions.Connectors.Abstractions;

/// <summary>
/// A connector with explicit connect/disconnect lifecycle and message send/receive.
/// </summary>
public interface IMessageConnector : IConnector
{
    /// <summary>
    /// Gets whether the connector is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the external system.
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnects from the external system.
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a message to the given destination.
    /// </summary>
    Task SendAsync(string destination, byte[] payload, IDictionary<string, string>? headers = null, CancellationToken ct = default);

    /// <summary>
    /// Receives the next message from the given source, with optional timeout.
    /// </summary>
    Task<ConnectorMessage?> ReceiveAsync(string source, TimeSpan? timeout = null, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to messages from the given source.
    /// </summary>
    Task SubscribeAsync(string source, Func<ConnectorMessage, Task> handler, CancellationToken ct = default);
}

/// <summary>
/// A message received from or sent to a connector.
/// </summary>
public sealed class ConnectorMessage
{
    /// <summary>Gets or sets the source/destination identifier.</summary>
    public string Source { get; set; } = "";

    /// <summary>Gets or sets the message payload.</summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    /// <summary>Gets or sets the message headers.</summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
