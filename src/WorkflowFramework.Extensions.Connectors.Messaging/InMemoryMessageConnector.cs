using System.Collections.Concurrent;
using WorkflowFramework.Extensions.Connectors.Abstractions;

namespace WorkflowFramework.Extensions.Connectors.Messaging;

/// <summary>
/// In-memory implementation of <see cref="IMessageConnector"/> for testing.
/// </summary>
public sealed class InMemoryMessageConnector : IMessageConnector
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ConnectorMessage>> _queues = new();
    private readonly ConcurrentDictionary<string, List<Func<ConnectorMessage, Task>>> _subscribers = new();
    private volatile bool _connected;

    /// <summary>
    /// Initializes a new instance of <see cref="InMemoryMessageConnector"/>.
    /// </summary>
    public InMemoryMessageConnector(string name = "in-memory")
    {
        Name = name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Type => "InMemory";

    /// <inheritdoc />
    public bool IsConnected => _connected;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken ct = default)
    {
        _connected = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _connected = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_connected);
    }

    /// <inheritdoc />
    public async Task SendAsync(string destination, byte[] payload, IDictionary<string, string>? headers = null, CancellationToken ct = default)
    {
        if (!_connected)
            throw new InvalidOperationException("Connector is not connected.");

        var message = new ConnectorMessage
        {
            Source = destination,
            Payload = payload,
            Headers = headers != null ? new Dictionary<string, string>(headers) : new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var queue = _queues.GetOrAdd(destination, _ => new ConcurrentQueue<ConnectorMessage>());
        queue.Enqueue(message);

        // Notify subscribers
        if (_subscribers.TryGetValue(destination, out var handlers))
        {
            foreach (var handler in handlers)
            {
                await handler(message).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task<ConnectorMessage?> ReceiveAsync(string source, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        if (!_connected)
            throw new InvalidOperationException("Connector is not connected.");

        var queue = _queues.GetOrAdd(source, _ => new ConcurrentQueue<ConnectorMessage>());
        var deadline = timeout.HasValue ? DateTimeOffset.UtcNow + timeout.Value : DateTimeOffset.MaxValue;

        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (queue.TryDequeue(out var message))
                return message;

            await Task.Delay(10, ct).ConfigureAwait(false);
        }

        return null;
    }

    /// <inheritdoc />
    public Task SubscribeAsync(string source, Func<ConnectorMessage, Task> handler, CancellationToken ct = default)
    {
        if (!_connected)
            throw new InvalidOperationException("Connector is not connected.");

        var handlers = _subscribers.GetOrAdd(source, _ => new List<Func<ConnectorMessage, Task>>());
        lock (handlers)
        {
            handlers.Add(handler);
        }

        return Task.CompletedTask;
    }
}
