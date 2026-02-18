namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Stores messages that could not be processed (dead letters).
/// </summary>
public interface IDeadLetterStore
{
    /// <summary>
    /// Sends a failed message to the dead letter store.
    /// </summary>
    /// <param name="message">The message that failed processing.</param>
    /// <param name="reason">The reason for failure.</param>
    /// <param name="exception">The exception that caused the failure, if any.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(object message, string reason, Exception? exception = null, CancellationToken cancellationToken = default);
}
