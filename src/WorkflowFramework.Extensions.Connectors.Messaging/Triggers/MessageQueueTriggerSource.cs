#if !NETSTANDARD2_0
using WorkflowFramework.Extensions.Connectors.Abstractions;
using WorkflowFramework.Triggers;

namespace WorkflowFramework.Extensions.Connectors.Messaging.Triggers;

/// <summary>
/// Trigger that fires when a message arrives on a queue/topic.
/// Config keys: "source" (required, queue/topic name).
/// </summary>
public sealed class MessageQueueTriggerSource : ITriggerSource
{
    private readonly TriggerDefinition _definition;
    private readonly IMessageConnector _connector;
    private TriggerContext? _context;
    private CancellationTokenSource? _cts;

    public MessageQueueTriggerSource(TriggerDefinition definition, IMessageConnector? connector)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _connector = connector ?? throw new InvalidOperationException(
            "MessageQueueTriggerSource requires an IMessageConnector. Register one in DI.");
    }

    public string Type => "queue";
    public string DisplayName => "Message Queue";
    public bool IsRunning { get; private set; }

    public async Task StartAsync(TriggerContext context, CancellationToken ct = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        _context = context;

        var config = context.Configuration;
        if (!config.TryGetValue("source", out var source) || string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("MessageQueueTriggerSource requires 'source' in configuration.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (!_connector.IsConnected)
            await _connector.ConnectAsync(_cts.Token).ConfigureAwait(false);

        await _connector.SubscribeAsync(source, OnMessageReceived, _cts.Token).ConfigureAwait(false);

        IsRunning = true;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        IsRunning = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsRunning = false;
        return default;
    }

    private async Task OnMessageReceived(ConnectorMessage message)
    {
        if (_context is null) return;

        try
        {
            var bodyText = System.Text.Encoding.UTF8.GetString(message.Payload);
            var headers = new Dictionary<string, object>();
            foreach (var h in message.Headers)
                headers[h.Key] = h.Value;

            await _context.OnTriggered(new TriggerEvent
            {
                TriggerType = Type,
                Timestamp = DateTimeOffset.UtcNow,
                Payload = new Dictionary<string, object>
                {
                    ["body"] = bodyText,
                    ["headers"] = headers,
                    ["source"] = message.Source
                }
            }).ConfigureAwait(false);
        }
        catch
        {
            // Swallow
        }
    }
}

#endif
