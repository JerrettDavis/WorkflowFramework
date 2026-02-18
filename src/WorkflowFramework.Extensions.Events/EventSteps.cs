namespace WorkflowFramework.Extensions.Events;

/// <summary>
/// A workflow step that publishes an event.
/// </summary>
public sealed class PublishEventStep : IStep
{
    private readonly IEventBus _eventBus;
    private readonly Func<IWorkflowContext, WorkflowEvent> _eventFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="PublishEventStep"/>.
    /// </summary>
    /// <param name="eventBus">The event bus.</param>
    /// <param name="eventFactory">Factory to create the event from workflow context.</param>
    /// <param name="name">Optional step name.</param>
    public PublishEventStep(IEventBus eventBus, Func<IWorkflowContext, WorkflowEvent> eventFactory, string? name = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _eventFactory = eventFactory ?? throw new ArgumentNullException(nameof(eventFactory));
        Name = name ?? "PublishEvent";
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var evt = _eventFactory(context);
        await _eventBus.PublishAsync(evt, context.CancellationToken).ConfigureAwait(false);
        context.Properties[$"{Name}.EventId"] = evt.Id;
    }
}

/// <summary>
/// A workflow step that waits for an event matching a correlation ID.
/// </summary>
public sealed class WaitForEventStep : IStep
{
    private readonly IEventBus _eventBus;
    private readonly string _eventType;
    private readonly Func<IWorkflowContext, string> _correlationIdFactory;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of <see cref="WaitForEventStep"/>.
    /// </summary>
    /// <param name="eventBus">The event bus.</param>
    /// <param name="eventType">The event type to wait for.</param>
    /// <param name="correlationIdFactory">Factory to get correlation ID from context.</param>
    /// <param name="timeout">Maximum wait time.</param>
    /// <param name="name">Optional step name.</param>
    public WaitForEventStep(IEventBus eventBus, string eventType, Func<IWorkflowContext, string> correlationIdFactory, TimeSpan timeout, string? name = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _eventType = eventType;
        _correlationIdFactory = correlationIdFactory ?? throw new ArgumentNullException(nameof(correlationIdFactory));
        _timeout = timeout;
        Name = name ?? $"WaitFor({eventType})";
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var correlationId = _correlationIdFactory(context);
        var evt = await _eventBus.WaitForEventAsync(_eventType, correlationId, _timeout, context.CancellationToken).ConfigureAwait(false);
        context.Properties[$"{Name}.Received"] = evt != null;
        if (evt != null)
        {
            context.Properties[$"{Name}.EventId"] = evt.Id;
            foreach (var kv in evt.Payload)
                context.Properties[$"{Name}.{kv.Key}"] = kv.Value;
        }
    }
}
