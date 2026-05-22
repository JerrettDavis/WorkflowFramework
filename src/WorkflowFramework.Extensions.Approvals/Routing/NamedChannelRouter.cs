namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// A registry-based <see cref="IApprovalRouter"/> that selects a channel by name from a
/// collection of registered <see cref="IApprovalChannel"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Resolution order:
/// <list type="number">
///   <item>If the request's <see cref="ApprovalRequest.Context"/> contains a key named
///   <c>"channel"</c>, the value is compared (case-insensitively) against each registered
///   channel's <see cref="IApprovalChannel.Name"/>. The first match is returned.</item>
///   <item>If no context key is present, or no channel name matches, the first registered
///   channel is returned as the default.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class NamedChannelRouter : IApprovalRouter
{
    private readonly IReadOnlyList<IApprovalChannel> _channels;

    /// <summary>
    /// Initialises a new instance of <see cref="NamedChannelRouter"/> with the
    /// given set of channels.
    /// </summary>
    /// <param name="channels">
    /// All registered <see cref="IApprovalChannel"/> instances. Must contain at least one entry.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="channels"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="channels"/> is empty.</exception>
    public NamedChannelRouter(IEnumerable<IApprovalChannel> channels)
    {
        if (channels is null) throw new ArgumentNullException(nameof(channels));

        _channels = channels.ToList().AsReadOnly();

        if (_channels.Count == 0)
            throw new ArgumentException("At least one approval channel must be registered.", nameof(channels));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reads the <c>"channel"</c> context key from the request and returns the first
    /// channel whose <see cref="IApprovalChannel.Name"/> matches (case-insensitive).
    /// Falls back to the first registered channel when no match is found.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    public IApprovalChannel Resolve(ApprovalRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (request.Context.TryGetValue("channel", out var channelObj) &&
            channelObj is string channelName &&
            !string.IsNullOrWhiteSpace(channelName))
        {
            var match = _channels.FirstOrDefault(c =>
                string.Equals(c.Name, channelName, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                return match;
        }

        // Default: first registered channel.
        return _channels[0];
    }
}
