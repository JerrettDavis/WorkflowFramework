namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Stores and retrieves large payloads using claim check pattern.
/// </summary>
public interface IClaimCheckStore
{
    /// <summary>
    /// Stores a payload and returns a claim ticket identifier.
    /// </summary>
    /// <param name="payload">The payload to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A claim ticket that can be used to retrieve the payload later.</returns>
    Task<string> StoreAsync(object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a payload using its claim ticket.
    /// </summary>
    /// <param name="claimTicket">The claim ticket identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored payload.</returns>
    Task<object> RetrieveAsync(string claimTicket, CancellationToken cancellationToken = default);
}
