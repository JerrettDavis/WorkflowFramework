# Connectors

WorkflowFramework provides a connector abstraction layer for integrating with external systems.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Connectors.Abstractions
dotnet add package WorkflowFramework.Extensions.Connectors.Grpc        # gRPC connector
dotnet add package WorkflowFramework.Extensions.Connectors.Messaging   # RabbitMQ, Kafka, Azure SB
```

## IConnector Interface

```csharp
using WorkflowFramework.Extensions.Connectors.Abstractions;

public interface IConnector
{
    string Name { get; }
    string Type { get; }
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}

// Typed variants:
public interface ISourceConnector<T> : IConnector
{
    Task<T> ReadAsync(CancellationToken ct = default);
}

public interface ISinkConnector<in T> : IConnector
{
    Task WriteAsync(T data, CancellationToken ct = default);
}

public interface IBidirectionalConnector<T> : ISourceConnector<T>, ISinkConnector<T> { }
```

## ConnectorRegistry

Register and resolve connectors by name:

```csharp
var registry = new ConnectorRegistry();
registry.Register(myHttpConnector);
registry.Register(mySqlConnector);

var connector = registry.Get("my-api");
var healthy = await connector!.TestConnectionAsync();
```

## ConnectorConfiguration

All connectors share a common configuration model:

```csharp
var config = new ConnectorConfiguration
{
    Name = "orders-api",
    Type = "Http",
    ConnectionString = "https://api.example.com",
    Authentication = new AuthenticationConfig
    {
        Type = "Bearer",
        Credentials = { ["token"] = "sk-..." }
    },
    Retry = new RetryConfig
    {
        MaxAttempts = 3,
        InitialDelay = TimeSpan.FromSeconds(1),
        ExponentialBackoff = true
    }
};
```

## Built-In Connector Types

- **File** — `FileConnectorConfig` for local filesystem access
- **HTTP** — REST API calls with auth and retry
- **SQL** — Database connectivity
- **FTP** — File transfer

## gRPC Connector

The `Connectors.Grpc` package provides gRPC channel integration.

## Messaging Connectors

The `Connectors.Messaging` package supports:

- **RabbitMQ** — publish/subscribe with exchanges and queues
- **Kafka** — topic-based produce/consume
- **Azure Service Bus** — queues and topics

> [!TIP]
> Combine connectors with the [integration patterns](integration-patterns.md) package for full EIP-style messaging workflows.
