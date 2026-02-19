using WorkflowFramework.Extensions.Connectors.Abstractions;

namespace WorkflowFramework.Extensions.Connectors.Grpc;

/// <summary>
/// Abstract gRPC connector for invoking gRPC services.
/// Implementations should provide the actual gRPC client.
/// </summary>
public interface IGrpcConnector : IConnector
{
    /// <summary>
    /// Invokes a unary gRPC method.
    /// </summary>
    /// <param name="serviceName">The fully-qualified service name.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="requestPayload">The request payload (serialized protobuf or JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response payload.</returns>
    Task<string> InvokeUnaryAsync(
        string serviceName,
        string methodName,
        string requestPayload,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for creating and managing gRPC channels.
/// </summary>
public interface IGrpcChannelFactory
{
    /// <summary>
    /// Gets or creates a channel for the specified address.
    /// </summary>
    /// <param name="address">The gRPC server address.</param>
    /// <returns>An opaque channel handle.</returns>
    object GetOrCreateChannel(string address);

    /// <summary>
    /// Disposes the channel for the specified address.
    /// </summary>
    /// <param name="address">The gRPC server address.</param>
    void DisposeChannel(string address);
}

/// <summary>
/// Configuration for gRPC connectors.
/// </summary>
public sealed class GrpcConnectorConfig : ConnectorConfiguration
{
    /// <summary>
    /// Gets or sets the gRPC server address.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to use TLS.
    /// </summary>
    public bool UseTls { get; set; } = true;

    /// <summary>
    /// Gets or sets the call deadline.
    /// </summary>
    public TimeSpan Deadline { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the max message size in bytes.
    /// </summary>
    public int MaxMessageSize { get; set; } = 4 * 1024 * 1024;
}
