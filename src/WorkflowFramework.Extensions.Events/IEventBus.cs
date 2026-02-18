namespace WorkflowFramework.Extensions.Events;

/// <summary>
/// Abstraction for publishing and subscribing to workflow events.
/// </summary>
public interface IEventBus
{
    /// <summary>Publishes an event.</summary>
    /// <param name="evt">The event to publish.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PublishAsync(WorkflowEvent evt, CancellationToken cancellationToken = default);

    /// <summary>Subscribes to events matching the given event type.</summary>
    /// <param name="eventType">The event type to subscribe to.</param>
    /// <param name="handler">The event handler.</param>
    /// <returns>A disposable subscription.</returns>
    IDisposable Subscribe(string eventType, Func<WorkflowEvent, Task> handler);

    /// <summary>
    /// Waits for an event matching the given type and correlation ID.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="correlationId">The correlation ID to match.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching event, or null if timed out.</returns>
    Task<WorkflowEvent?> WaitForEventAsync(string eventType, string correlationId, TimeSpan timeout, CancellationToken cancellationToken = default);
}
