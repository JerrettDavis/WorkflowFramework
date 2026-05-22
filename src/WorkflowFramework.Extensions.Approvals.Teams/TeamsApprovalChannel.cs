using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Approvals.Teams.Cards;

namespace WorkflowFramework.Extensions.Approvals.Teams;

/// <summary>
/// An <see cref="IApprovalChannel"/> implementation that posts Adaptive Card v1.5
/// messages to Microsoft Teams and waits for button-based responses routed back via
/// the configured callback endpoint.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="TeamsApprovalMode.IncomingWebhook"/> is configured the card is
/// posted directly to the webhook URL.  Note that incoming-webhook connectors are
/// <b>send-only</b>: Teams does not route <c>Action.Submit</c> callbacks from cards
/// posted via this mechanism back to an arbitrary HTTPS endpoint.  In practice this
/// means the channel will block until the timeout elapses unless votes are submitted
/// via another integration path (e.g., a Power Automate flow that POSTs to
/// <see cref="TeamsApprovalOptions.CallbackPath"/>).
/// </para>
/// <para>
/// When <see cref="TeamsApprovalMode.Bot"/> is configured the message is sent via the
/// Bot Framework conversation API.  <b>This is a minimal / placeholder implementation.</b>
/// It uses <see cref="TeamsApprovalOptions.BotAppPassword"/> as a bearer token directly.
/// A production deployment must acquire an OAuth 2.0 access token via the Azure AD
/// client-credentials flow before calling the Bot Framework API.
/// </para>
/// </remarks>
public sealed class TeamsApprovalChannel : IApprovalChannel
{
    /// <summary>The stable channel identifier returned by <see cref="Name"/>.</summary>
    internal const string ChannelName = "teams";

    private readonly TeamsApprovalOptions _options;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IApprovalStore _store;
    private readonly Lazy<PersistentApprovalService> _persistent;
    private readonly TeamsCallbackTokenService _tokens;
    private readonly ILogger<TeamsApprovalChannel> _logger;

    /// <summary>
    /// Initialises a new <see cref="TeamsApprovalChannel"/>.
    /// </summary>
    /// <param name="options">Bound Teams options.</param>
    /// <param name="httpFactory">Factory used to create named HTTP clients.</param>
    /// <param name="store">The approval store for idempotency checks.</param>
    /// <param name="persistent">
    /// A lazy reference to the persistent approval service used to await completion.
    /// Lazy to break the DI circular dependency.
    /// </param>
    /// <param name="tokens">Token service for signing action data.</param>
    /// <param name="logger">Optional structured logger.</param>
    public TeamsApprovalChannel(
        IOptions<TeamsApprovalOptions> options,
        IHttpClientFactory httpFactory,
        IApprovalStore store,
        Lazy<PersistentApprovalService> persistent,
        TeamsCallbackTokenService tokens,
        ILogger<TeamsApprovalChannel>? logger = null)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        _options = options.Value;
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _persistent = persistent ?? throw new ArgumentNullException(nameof(persistent));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _logger = logger ?? NullLogger<TeamsApprovalChannel>.Instance;
    }

    /// <inheritdoc />
    public string Name => ChannelName;

    /// <inheritdoc />
    /// <remarks>
    /// <list type="number">
    ///   <item>Checks the store for an existing record (idempotency).</item>
    ///   <item>Builds signed approve and reject tokens expiring at <c>now + request.Timeout</c>.</item>
    ///   <item>Builds an Adaptive Card and wraps it in the Teams message envelope.</item>
    ///   <item>POSTs the envelope to Teams (webhook or Bot Framework API).</item>
    ///   <item>Awaits completion via <see cref="PersistentApprovalService.WaitForCompletionAsync"/>.</item>
    /// </list>
    /// </remarks>
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        // Idempotency: skip posting if already in-flight.
        var existing = await _store.LoadAsync(request.CorrelationId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            var expiry = DateTimeOffset.UtcNow + request.Timeout;
            var approveToken = _tokens.Create(request.CorrelationId, true, expiry);
            var rejectToken = _tokens.Create(request.CorrelationId, false, expiry);

            var card = AdaptiveCardBuilder.BuildApprovalCard(request, approveToken, rejectToken);
            var envelope = AdaptiveCardBuilder.BuildMessageEnvelope(card);

            await PostToTeamsAsync(envelope, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Teams approval card posted for correlationId '{CorrelationId}'.",
                request.CorrelationId);
        }
        else
        {
            _logger.LogDebug(
                "Teams approval card already posted for correlationId '{CorrelationId}'; skipping re-post.",
                request.CorrelationId);
        }

        return await _persistent.Value.WaitForCompletionAsync(request.CorrelationId, cancellationToken)
            .ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task PostToTeamsAsync(JsonObject envelope, CancellationToken cancellationToken)
    {
        var client = _httpFactory.CreateClient(_options.HttpClientName);
        HttpResponseMessage response;

        if (_options.Mode == TeamsApprovalMode.IncomingWebhook)
        {
            response = await client
                .PostAsJsonAsync(_options.WebhookUrl, envelope, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // Bot mode (minimal / placeholder).
            // WARNING: This sends BotAppPassword directly as bearer token.
            // A production implementation must acquire an OAuth 2.0 token via
            // Azure AD client-credentials flow before calling this API.
            var url = $"{_options.BotServiceUrl!.TrimEnd('/')}/v3/conversations/{_options.BotConversationId}/activities";

            var botMessage = new JsonObject
            {
                ["type"] = "message",
                ["attachments"] = envelope["attachments"],
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.BotAppPassword);
            requestMessage.Content = JsonContent.Create(botMessage, options: new JsonSerializerOptions());

            response = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Teams API returned non-success status {(int)response.StatusCode}: {body}");
        }
    }
}
