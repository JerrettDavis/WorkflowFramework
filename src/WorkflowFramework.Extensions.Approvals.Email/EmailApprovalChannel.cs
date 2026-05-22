using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// An <see cref="IApprovalChannel"/> implementation that delivers approval requests via email.
/// Each recipient receives two HMAC-signed callback URLs — one to approve, one to reject.
/// Responses are collected via the ASP.NET Core endpoint registered by
/// <see cref="EmailEndpointExtensions.MapEmailApprovalCallback"/>.
/// </summary>
public sealed class EmailApprovalChannel : IApprovalChannel
{
    private readonly EmailApprovalOptions _options;
    private readonly IEmailSender _sender;
    private readonly IApprovalStore _store;
    private readonly Lazy<PersistentApprovalService> _persistent;
    private readonly ApprovalTokenService _tokens;
    private readonly ILogger<EmailApprovalChannel> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="EmailApprovalChannel"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="PersistentApprovalService"/> dependency is accepted as a
    /// <see cref="Lazy{T}"/> to break the DI circular reference that would otherwise arise when
    /// <c>PersistentApprovalService</c> depends on <c>IApprovalChannel</c>, which in turn
    /// resolves this channel.
    /// </para>
    /// </remarks>
    public EmailApprovalChannel(
        IOptions<EmailApprovalOptions> options,
        IEmailSender sender,
        IApprovalStore store,
        Lazy<PersistentApprovalService> persistent,
        ApprovalTokenService tokens,
        ILogger<EmailApprovalChannel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(persistent);
        ArgumentNullException.ThrowIfNull(tokens);

        _options = options.Value;
        _sender = sender;
        _store = store;
        _persistent = persistent;
        _tokens = tokens;
        _logger = logger ?? NullLogger<EmailApprovalChannel>.Instance;
    }

    /// <inheritdoc />
    public string Name => "email";

    /// <inheritdoc />
    /// <remarks>
    /// <list type="number">
    ///   <item>Resolves recipient addresses from <see cref="ApprovalRequest.Context"/> using <see cref="EmailApprovalOptions.RecipientsContextKey"/>.</item>
    ///   <item>Saves a <see cref="PendingApproval"/> to the store (idempotent).</item>
    ///   <item>Sends one email per recipient with signed approve/reject URLs; individual send failures are logged but do not abort the batch unless all sends fail.</item>
    ///   <item>Awaits the persistent approval service until an external resolver completes the correlation.</item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no recipients are found in context, or when all email sends fail.
    /// </exception>
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var recipients = EmailMessageBuilder.ResolveRecipients(request.Context, _options.RecipientsContextKey);

        if (recipients.Count == 0)
            throw new InvalidOperationException(
                "Email approval channel requires recipients in context. " +
                $"Ensure the context contains a '{_options.RecipientsContextKey}' key with one or more email addresses.");

        // Idempotent save: only save if not already present.
        var existing = await _store.LoadAsync(request.CorrelationId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            var now = DateTimeOffset.UtcNow;
            var pending = new PendingApproval(
                CorrelationId: request.CorrelationId,
                Request: request,
                PrimaryChannel: Name,
                CreatedAt: now,
                DeadlineAt: now + request.Timeout,
                Votes: Array.Empty<ApprovalRecord>(),
                EscalationChannel: null,
                TimeoutAction: OnTimeoutAction.AutoReject);

            await _store.SaveAsync(pending, cancellationToken).ConfigureAwait(false);
        }

        var tokenLifetime = _options.TokenLifetime ?? request.Timeout;
        var expiresAt = DateTimeOffset.UtcNow + tokenLifetime;

        var sendFailures = 0;
        foreach (var recipient in recipients)
        {
            try
            {
                var approveToken = _tokens.Create(new ApprovalTokenPayload(
                    CorrelationId: request.CorrelationId,
                    ApproverId: recipient,
                    Decision: true,
                    ExpiresAt: expiresAt,
                    ApproverDisplayName: recipient));

                var rejectToken = _tokens.Create(new ApprovalTokenPayload(
                    CorrelationId: request.CorrelationId,
                    ApproverId: recipient,
                    Decision: false,
                    ExpiresAt: expiresAt,
                    ApproverDisplayName: recipient));

                var approveUrl = _options.ApproveUrlTemplate.Replace("{token}", approveToken, StringComparison.OrdinalIgnoreCase);
                var rejectUrl = _options.RejectUrlTemplate.Replace("{token}", rejectToken, StringComparison.OrdinalIgnoreCase);

                var message = EmailMessageBuilder.Build(
                    _options,
                    recipient,
                    request.Title,
                    request.Description,
                    approveUrl,
                    rejectUrl);

                await _sender.SendAsync(message, cancellationToken).ConfigureAwait(false);

                _logger.LogDebug(
                    "Sent approval request email for correlation '{CorrelationId}' to '{Recipient}'.",
                    request.CorrelationId, recipient);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sendFailures++;
                _logger.LogWarning(ex,
                    "Failed to send approval email for correlation '{CorrelationId}' to '{Recipient}'.",
                    request.CorrelationId, recipient);
            }
        }

        if (sendFailures == recipients.Count)
            throw new InvalidOperationException(
                $"All {recipients.Count} email send attempts failed for correlation '{request.CorrelationId}'. " +
                "Check SMTP configuration and logs for details.");

        return await _persistent.Value.WaitForCompletionAsync(request.CorrelationId, cancellationToken).ConfigureAwait(false);
    }
}
