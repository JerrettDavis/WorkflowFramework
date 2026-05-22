using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WorkflowFramework.Extensions.Approvals.Slack;

/// <summary>
/// Handles incoming Slack interactivity payloads, validates signatures, extracts vote
/// information from action callbacks, and routes them to <see cref="PersistentApprovalService"/>.
/// </summary>
public sealed class SlackInteractivityHandler
{
    private readonly SlackSignatureValidator _signatures;
    private readonly PersistentApprovalService _persistent;
    private readonly ILogger<SlackInteractivityHandler> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="SlackInteractivityHandler"/>.
    /// </summary>
    /// <param name="signatures">The signature validator used to authenticate incoming requests.</param>
    /// <param name="persistent">The persistent approval service used to record votes.</param>
    /// <param name="logger">Optional logger. A no-op logger is used when <see langword="null"/>.</param>
    public SlackInteractivityHandler(
        SlackSignatureValidator signatures,
        PersistentApprovalService persistent,
        ILogger<SlackInteractivityHandler>? logger = null)
    {
        _signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
        _persistent = persistent ?? throw new ArgumentNullException(nameof(persistent));
        _logger = logger ?? NullLogger<SlackInteractivityHandler>.Instance;
    }

    /// <summary>
    /// Processes a Slack interactivity callback payload.
    /// </summary>
    /// <param name="timestamp">The <c>X-Slack-Request-Timestamp</c> header value.</param>
    /// <param name="signature">The <c>X-Slack-Signature</c> header value.</param>
    /// <param name="rawBody">
    /// The raw, unmodified request body (form-encoded with a single <c>payload</c> field
    /// containing URL-encoded JSON).
    /// </param>
    /// <param name="ct">Propagates notification that the operation should be cancelled.</param>
    /// <returns>
    /// A <see cref="SlackInteractionResult"/> with an HTTP status code and optional message.
    /// </returns>
    public async Task<SlackInteractionResult> HandleAsync(
        string timestamp,
        string signature,
        string rawBody,
        CancellationToken ct)
    {
        // Validate signature first
        if (!_signatures.Validate(timestamp, rawBody, signature))
        {
            _logger.LogWarning("Slack signature validation failed for incoming request.");
            return new SlackInteractionResult(401, "Invalid signature.");
        }

        // Parse form-encoded body: payload={url-encoded-json}
        string? payloadJson;
        try
        {
            var parsed = HttpUtility.ParseQueryString(rawBody);
            payloadJson = parsed["payload"];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse form-encoded body from Slack interactivity request.");
            return new SlackInteractionResult(400, "Malformed request body.");
        }

        if (string.IsNullOrEmpty(payloadJson))
        {
            _logger.LogWarning("Slack interactivity request missing 'payload' field.");
            return new SlackInteractionResult(400, "Missing payload field.");
        }

        // Parse JSON payload
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(payloadJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Slack payload JSON.");
            return new SlackInteractionResult(400, "Malformed payload JSON.");
        }

        if (root is null)
        {
            return new SlackInteractionResult(400, "Null payload.");
        }

        // Extract actions array
        var actionsNode = root["actions"];
        if (actionsNode is not JsonArray actions || actions.Count == 0)
        {
            _logger.LogWarning("Slack payload missing or empty 'actions' array.");
            return new SlackInteractionResult(400, "Missing actions.");
        }

        // Extract action_id from first action
        var actionId = actions[0]?["action_id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(actionId))
        {
            _logger.LogWarning("Slack action missing 'action_id'.");
            return new SlackInteractionResult(400, "Missing action_id.");
        }

        // Parse action_id: "approve:{correlationId}" or "reject:{correlationId}"
        var colonIndex = actionId.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex < 0)
        {
            _logger.LogWarning("Slack action_id '{ActionId}' has no colon separator.", actionId);
            return new SlackInteractionResult(400, "Invalid action_id format.");
        }

        var actionPrefix = actionId[..colonIndex];
        var correlationId = actionId[(colonIndex + 1)..];

        bool approved;
        if (actionPrefix == "approve")
            approved = true;
        else if (actionPrefix == "reject")
            approved = false;
        else
        {
            _logger.LogWarning("Unknown action prefix '{Prefix}' in action_id '{ActionId}'.", actionPrefix, actionId);
            return new SlackInteractionResult(400, $"Unknown action prefix: {actionPrefix}.");
        }

        // Extract user info
        var userId = root["user"]?["id"]?.GetValue<string>() ?? "unknown";
        var userName = root["user"]?["username"]?.GetValue<string>();

        var vote = new ApprovalRecord(
            ApproverId: userId,
            ApproverDisplayName: userName,
            Approved: approved,
            Comment: null,
            Timestamp: DateTimeOffset.UtcNow,
            Channel: "slack");

        try
        {
            await _persistent.ResolveExternalAsync(correlationId, vote, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Slack vote recorded: correlationId={CorrelationId}, approved={Approved}, user={UserId}",
                correlationId, approved, userId);
            return new SlackInteractionResult(200, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "User '{UserId}' is not authorized to vote on '{CorrelationId}'.", userId, correlationId);
            return new SlackInteractionResult(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Could not resolve approval for correlationId '{CorrelationId}'.", correlationId);
            return new SlackInteractionResult(404, ex.Message);
        }
    }
}

/// <summary>
/// Represents the result of handling a Slack interactivity callback, including an HTTP status
/// code and an optional message body.
/// </summary>
/// <param name="StatusCode">The HTTP status code to return to Slack.</param>
/// <param name="Message">An optional message body for error cases.</param>
public sealed record SlackInteractionResult(int StatusCode, string? Message);
