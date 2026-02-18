namespace WorkflowFramework.Extensions.Connectors.Abstractions;

/// <summary>
/// Base interface for all connectors.
/// </summary>
public interface IConnector
{
    /// <summary>
    /// Gets the unique name of this connector instance.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the connector type identifier (e.g., "File", "Http", "Sql").
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Tests the connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the connection is healthy.</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A connector that reads data from an external source.
/// </summary>
/// <typeparam name="T">The type of data read.</typeparam>
public interface ISourceConnector<T> : IConnector
{
    /// <summary>
    /// Reads data from the source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The data read from the source.</returns>
    Task<T> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A connector that writes data to an external destination.
/// </summary>
/// <typeparam name="T">The type of data to write.</typeparam>
public interface ISinkConnector<in T> : IConnector
{
    /// <summary>
    /// Writes data to the destination.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteAsync(T data, CancellationToken cancellationToken = default);
}

/// <summary>
/// A connector that both reads and writes data.
/// </summary>
/// <typeparam name="T">The type of data.</typeparam>
public interface IBidirectionalConnector<T> : ISourceConnector<T>, ISinkConnector<T>
{
}

/// <summary>
/// Factory for creating connectors from configuration.
/// </summary>
public interface IConnectorFactory
{
    /// <summary>
    /// Creates a connector from the given configuration.
    /// </summary>
    /// <param name="configuration">The connector configuration.</param>
    /// <returns>The created connector.</returns>
    IConnector Create(ConnectorConfiguration configuration);
}

/// <summary>
/// Discovers and manages available connectors.
/// </summary>
public interface IConnectorRegistry
{
    /// <summary>
    /// Gets a connector by name.
    /// </summary>
    /// <param name="name">The connector name.</param>
    /// <returns>The connector, or null if not found.</returns>
    IConnector? Get(string name);

    /// <summary>
    /// Registers a connector.
    /// </summary>
    /// <param name="connector">The connector to register.</param>
    void Register(IConnector connector);

    /// <summary>
    /// Gets all registered connector names.
    /// </summary>
    IEnumerable<string> Names { get; }
}
