using FluentAssertions;
using WorkflowFramework.Extensions.Connectors.Messaging.Abstractions;
using WorkflowFramework.Extensions.Connectors.Messaging.Configuration;
using Xunit;

namespace WorkflowFramework.Tests.Connectors;

public class MessagingConnectorTests
{
    #region BrokerMessage

    [Fact]
    public void BrokerMessage_DefaultValues()
    {
        var msg = new BrokerMessage();
        msg.Id.Should().NotBeNullOrEmpty();
        msg.Body.Should().BeEmpty();
        msg.ContentType.Should().Be("application/json");
        msg.Headers.Should().BeEmpty();
        msg.RoutingKey.Should().BeNull();
        msg.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void BrokerMessage_SetProperties()
    {
        var msg = new BrokerMessage
        {
            Id = "custom-id",
            Body = "{\"key\":\"val\"}",
            ContentType = "text/plain",
            RoutingKey = "orders",
            CorrelationId = "corr-1"
        };
        msg.Headers["custom"] = "header";
        msg.Id.Should().Be("custom-id");
        msg.Body.Should().Be("{\"key\":\"val\"}");
        msg.RoutingKey.Should().Be("orders");
        msg.Headers.Should().ContainKey("custom");
    }

    #endregion

    #region RabbitMQ Config

    [Fact]
    public void RabbitMqConfig_Defaults()
    {
        var config = new RabbitMqConnectorConfig();
        config.Exchange.Should().BeEmpty();
        config.Queue.Should().BeEmpty();
        config.RoutingKey.Should().BeEmpty();
        config.Durable.Should().BeTrue();
        config.PrefetchCount.Should().Be(10);
    }

    [Fact]
    public void RabbitMqConfig_SetProperties()
    {
        var config = new RabbitMqConnectorConfig
        {
            Exchange = "my-exchange",
            Queue = "my-queue",
            RoutingKey = "orders.created",
            Durable = false,
            PrefetchCount = 50,
            Name = "rabbitmq1",
            ConnectionString = "amqp://localhost"
        };
        config.Exchange.Should().Be("my-exchange");
        config.Name.Should().Be("rabbitmq1");
    }

    #endregion

    #region Azure Service Bus Config

    [Fact]
    public void AzureServiceBusConfig_Defaults()
    {
        var config = new AzureServiceBusConnectorConfig();
        config.QueueOrTopicName.Should().BeEmpty();
        config.SubscriptionName.Should().BeNull();
        config.MaxConcurrentCalls.Should().Be(1);
        config.AutoComplete.Should().BeTrue();
    }

    [Fact]
    public void AzureServiceBusConfig_SetProperties()
    {
        var config = new AzureServiceBusConnectorConfig
        {
            QueueOrTopicName = "orders",
            SubscriptionName = "sub1",
            MaxConcurrentCalls = 10,
            AutoComplete = false
        };
        config.QueueOrTopicName.Should().Be("orders");
        config.SubscriptionName.Should().Be("sub1");
    }

    #endregion

    #region Kafka Config

    [Fact]
    public void KafkaConfig_Defaults()
    {
        var config = new KafkaConnectorConfig();
        config.Topic.Should().BeEmpty();
        config.GroupId.Should().BeEmpty();
        config.BootstrapServers.Should().BeEmpty();
        config.AutoOffsetReset.Should().Be("earliest");
    }

    [Fact]
    public void KafkaConfig_SetProperties()
    {
        var config = new KafkaConnectorConfig
        {
            Topic = "events",
            GroupId = "consumer-group-1",
            BootstrapServers = "localhost:9092",
            AutoOffsetReset = "latest"
        };
        config.Topic.Should().Be("events");
        config.GroupId.Should().Be("consumer-group-1");
    }

    #endregion
}
