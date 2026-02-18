namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Bridges between a workflow and an external messaging system.
/// </summary>
public interface IChannelAdapter
{
    /// <summary>
    /// Sends a message to the external channel.
    /// </summary>
    /// <param name="message">The message payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(object message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives a message from the external channel.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The received message, or null if none available.</returns>
    Task<object?> ReceiveAsync(CancellationToken cancellationToken = default);
}
