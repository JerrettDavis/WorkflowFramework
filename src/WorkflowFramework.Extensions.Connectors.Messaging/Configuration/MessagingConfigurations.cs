using WorkflowFramework.Extensions.Connectors.Abstractions;

namespace WorkflowFramework.Extensions.Connectors.Messaging.Configuration;

/// <summary>
/// Configuration for RabbitMQ connectors.
/// </summary>
public sealed class RabbitMqConnectorConfig : ConnectorConfiguration
{
    /// <summary>
    /// Gets or sets the exchange name.
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string Queue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the routing key.
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the queue should be durable.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Gets or sets the prefetch count.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;
}

/// <summary>
/// Configuration for Azure Service Bus connectors.
/// </summary>
public sealed class AzureServiceBusConnectorConfig : ConnectorConfiguration
{
    /// <summary>
    /// Gets or sets the queue or topic name.
    /// </summary>
    public string QueueOrTopicName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subscription name (for topic subscriptions).
    /// </summary>
    public string? SubscriptionName { get; set; }

    /// <summary>
    /// Gets or sets the max concurrent calls.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether to auto-complete messages.
    /// </summary>
    public bool AutoComplete { get; set; } = true;
}

/// <summary>
/// Configuration for Kafka connectors.
/// </summary>
public sealed class KafkaConnectorConfig : ConnectorConfiguration
{
    /// <summary>
    /// Gets or sets the topic name.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the consumer group ID.
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bootstrap servers.
    /// </summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the auto-offset reset policy.
    /// </summary>
    public string AutoOffsetReset { get; set; } = "earliest";
}
