using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Teams;

/// <summary>
/// Handles inbound Teams <c>Action.Submit</c> callback payloads, verifies the
/// embedded HMAC token, and submits the vote to <see cref="PersistentApprovalService"/>.
/// </summary>
/// <remarks>
/// Expected payload shape (posted by the Bot Framework or an outgoing-webhook relay):
/// <code>
/// {
///   "from": { "id": "user1@tenant", "name": "Alice" },
///   "value": { "correlationId": "abc", "decision": "approve", "token": "..." }
/// }
/// </code>
/// </remarks>
public sealed class TeamsCallbackHandler
{
    private readonly TeamsCallbackTokenService _tokens;
    private readonly PersistentApprovalService _persistent;
    private readonly ILogger<TeamsCallbackHandler> _logger;

    /// <summary>
    /// Initialises a new <see cref="TeamsCallbackHandler"/>.
    /// </summary>
    /// <param name="tokens">The token service used to verify action data tokens.</param>
    /// <param name="persistent">The persistent approval service used to submit votes.</param>
    /// <param name="logger">Optional structured logger.</param>
    public TeamsCallbackHandler(
        TeamsCallbackTokenService tokens,
        PersistentApprovalService persistent,
        ILogger<TeamsCallbackHandler>? logger = null)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _persistent = persistent ?? throw new ArgumentNullException(nameof(persistent));
        _logger = logger ?? NullLogger<TeamsCallbackHandler>.Instance;
    }

    /// <summary>
    /// Processes a callback payload received from Teams and records the vote.
    /// </summary>
    /// <param name="payload">The parsed JSON payload from the Teams callback request.</param>
    /// <param name="ct">A token that can cancel the operation.</param>
    /// <returns>A <see cref="TeamsCallbackResult"/> indicating the HTTP response to return.</returns>
    public async Task<TeamsCallbackResult> HandleAsync(JsonNode? payload, CancellationToken ct)
    {
        if (payload is null)
            return new TeamsCallbackResult(400, "Payload is missing.");

        try
        {
            // Extract from/value fields.
            var fromNode = payload["from"];
            var valueNode = payload["value"];

            if (valueNode is null)
                return new TeamsCallbackResult(400, "Payload is missing the 'value' field.");

            var correlationId = valueNode["correlationId"]?.GetValue<string>();
            var decisionStr = valueNode["decision"]?.GetValue<string>();
            var token = valueNode["token"]?.GetValue<string>();

            if (string.IsNullOrEmpty(correlationId) ||
                string.IsNullOrEmpty(decisionStr) ||
                string.IsNullOrEmpty(token))
            {
                return new TeamsCallbackResult(400, "Payload 'value' must contain correlationId, decision, and token.");
            }

            // Verify the HMAC token.
            if (!_tokens.TryVerify(token, out var tokenCorrelationId, out var tokenDecision, out _))
            {
                _logger.LogWarning("Teams callback received invalid or expired token for correlationId '{CorrelationId}'.", correlationId);
                return new TeamsCallbackResult(401, "Token is invalid or has expired.");
            }

            // Ensure token correlationId matches payload correlationId.
            if (!string.Equals(tokenCorrelationId, correlationId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Token correlationId '{TokenCorrelationId}' does not match payload correlationId '{PayloadCorrelationId}'.",
                    tokenCorrelationId, correlationId);
                return new TeamsCallbackResult(401, "Token correlationId does not match payload.");
            }

            // Determine decision from payload and verify it matches the token.
            var payloadDecision = string.Equals(decisionStr, "approve", StringComparison.OrdinalIgnoreCase);
            if (tokenDecision != payloadDecision)
            {
                _logger.LogWarning(
                    "Token decision '{TokenDecision}' does not match payload decision '{PayloadDecision}' for correlationId '{CorrelationId}'.",
                    tokenDecision, payloadDecision, correlationId);
                return new TeamsCallbackResult(401, "Token decision does not match payload decision.");
            }

            // Build the ApprovalRecord.
            var approverId = fromNode?["id"]?.GetValue<string>() ?? "unknown";
            var approverName = fromNode?["name"]?.GetValue<string>();

            var record = new ApprovalRecord(
                ApproverId: approverId,
                ApproverDisplayName: approverName,
                Approved: payloadDecision,
                Comment: null,
                Timestamp: DateTimeOffset.UtcNow,
                Channel: TeamsApprovalChannel.ChannelName);

            await _persistent.ResolveExternalAsync(correlationId, record, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Teams callback processed: correlationId '{CorrelationId}', decision '{Decision}', approver '{ApproverId}'.",
                correlationId, decisionStr, approverId);

            return new TeamsCallbackResult(200, "Vote recorded.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Teams callback rejected: approver not in allowed roles.");
            return new TeamsCallbackResult(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Teams callback rejected: approval not found or already complete.");
            return new TeamsCallbackResult(404, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing Teams callback.");
            return new TeamsCallbackResult(500, "An unexpected error occurred.");
        }
    }
}
