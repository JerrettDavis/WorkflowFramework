using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkflowFramework.Extensions.Approvals.Slack.Blocks;

namespace WorkflowFramework.Extensions.Approvals.Slack;

/// <summary>
/// An <see cref="IApprovalChannel"/> implementation that sends approval requests to a Slack
/// channel using Block Kit messages with interactive Approve/Reject buttons. Responses are
/// collected via the Slack interactivity callback endpoint.
/// </summary>
public sealed class SlackApprovalChannel : IApprovalChannel
{
    private readonly SlackApprovalOptions _options;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IApprovalStore _store;
    private readonly Lazy<PersistentApprovalService> _persistent;
    private readonly ILogger<SlackApprovalChannel> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="SlackApprovalChannel"/>.
    /// </summary>
    /// <param name="options">Slack approval channel configuration.</param>
    /// <param name="httpFactory">Factory for creating named <see cref="System.Net.Http.HttpClient"/> instances.</param>
    /// <param name="store">The approval store for persisting pending approvals.</param>
    /// <param name="persistent">
    /// A lazy reference to the persistent approval service used to wait for external votes.
    /// Lazy to break the DI circular dependency.
    /// </param>
    /// <param name="logger">Optional logger. A no-op logger is used when <see langword="null"/>.</param>
    public SlackApprovalChannel(
        IOptions<SlackApprovalOptions> options,
        IHttpClientFactory httpFactory,
        IApprovalStore store,
        Lazy<PersistentApprovalService> persistent,
        ILogger<SlackApprovalChannel>? logger = null)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _persistent = persistent ?? throw new ArgumentNullException(nameof(persistent));
        _logger = logger ?? NullLogger<SlackApprovalChannel>.Instance;
    }

    /// <inheritdoc />
    public string Name => "slack";

    /// <inheritdoc />
    /// <remarks>
    /// <list type="number">
    ///   <item>Checks the store for an existing pending approval (idempotency).</item>
    ///   <item>Saves a pending approval to the store if not already present.</item>
    ///   <item>Builds a Block Kit message via <see cref="SlackBlockKitBuilder"/>.</item>
    ///   <item>POSTs the message to <c>{ApiBaseUrl}chat.postMessage</c>.</item>
    ///   <item>Throws <see cref="InvalidOperationException"/> if Slack returns <c>ok=false</c>.</item>
    ///   <item>Awaits <see cref="PersistentApprovalService.WaitForCompletionAsync"/> until a vote resolves the request.</item>
    /// </list>
    /// </remarks>
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        // Idempotency: check if a pending approval already exists
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

        // Build and post Block Kit message
        var payload = SlackBlockKitBuilder.BuildApprovalMessage(_options.ChannelId, request);
        var json = payload.ToJsonString();

        var client = _httpFactory.CreateClient(_options.HttpClientName);
        var apiUrl = _options.ApiBaseUrl.TrimEnd('/') + "/chat.postMessage";

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = content
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BotToken);

        _logger.LogDebug("Posting approval message to Slack for correlationId={CorrelationId}.", request.CorrelationId);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            JsonNode? responseJson;
            try
            {
                responseJson = JsonNode.Parse(responseBody);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException(
                    $"Slack API returned non-JSON response (HTTP {(int)response.StatusCode}): {responseBody}");
            }

            var ok = responseJson?["ok"]?.GetValue<bool>() ?? false;
            if (!ok)
            {
                var error = responseJson?["error"]?.GetValue<string>() ?? "unknown_error";
                _logger.LogError(
                    "Slack chat.postMessage failed for correlationId={CorrelationId}: {Error}",
                    request.CorrelationId, error);
                throw new InvalidOperationException(
                    $"Slack API error for correlationId '{request.CorrelationId}': {error}");
            }

            var channel = responseJson?["channel"]?.GetValue<string>();
            var ts = responseJson?["ts"]?.GetValue<string>();
            _logger.LogInformation(
                "Approval message posted to Slack: correlationId={CorrelationId}, channel={Channel}, ts={Ts}",
                request.CorrelationId, channel, ts);
        }

        return await _persistent.Value.WaitForCompletionAsync(request.CorrelationId, cancellationToken)
            .ConfigureAwait(false);
    }
}
