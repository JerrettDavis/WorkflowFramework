using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.Connectors.Abstractions;

/// <summary>
/// Default implementation of <see cref="IConnectorRegistry"/>.
/// </summary>
public sealed class ConnectorRegistry : IConnectorRegistry
{
    private readonly ConcurrentDictionary<string, IConnector> _connectors = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IConnector? Get(string name) =>
        _connectors.TryGetValue(name, out var connector) ? connector : null;

    /// <inheritdoc />
    public void Register(IConnector connector)
    {
        if (connector == null) throw new ArgumentNullException(nameof(connector));
        if (!_connectors.TryAdd(connector.Name, connector))
            throw new InvalidOperationException($"A connector named '{connector.Name}' is already registered.");
    }

    /// <inheritdoc />
    public IEnumerable<string> Names => _connectors.Keys;
}
