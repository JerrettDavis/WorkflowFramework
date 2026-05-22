using System.Text.Json.Nodes;
using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Teams.Cards;

/// <summary>
/// Builds Adaptive Card v1.5 payloads for approval requests and wraps them in
/// the Teams incoming-webhook message envelope.
/// </summary>
public static class AdaptiveCardBuilder
{
    private const string CardSchema = "http://adaptivecards.io/schemas/adaptive-card.json";
    private const string CardVersion = "1.5";

    /// <summary>
    /// Builds a complete Adaptive Card v1.5 JSON object for the given approval request.
    /// </summary>
    /// <param name="request">The approval request to render.</param>
    /// <param name="approveActionToken">The signed HMAC token to embed in the Approve button.</param>
    /// <param name="rejectActionToken">The signed HMAC token to embed in the Reject button.</param>
    /// <returns>A <see cref="JsonObject"/> representing the Adaptive Card.</returns>
    public static JsonObject BuildApprovalCard(
        ApprovalRequest request,
        string approveActionToken,
        string rejectActionToken)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (approveActionToken is null) throw new ArgumentNullException(nameof(approveActionToken));
        if (rejectActionToken is null) throw new ArgumentNullException(nameof(rejectActionToken));

        var body = new JsonArray
        {
            // Title
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = request.Title,
                ["size"] = "Large",
                ["weight"] = "Bolder",
                ["wrap"] = true,
            },
        };

        // Description (optional)
        if (!string.IsNullOrEmpty(request.Description))
        {
            body.Add(new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = request.Description,
                ["wrap"] = true,
            });
        }

        // FactSet: context entries + metadata
        var facts = new JsonArray();

        foreach (var kvp in request.Context)
        {
            facts.Add(new JsonObject
            {
                ["title"] = kvp.Key,
                ["value"] = kvp.Value?.ToString() ?? string.Empty,
            });
        }

        facts.Add(new JsonObject
        {
            ["title"] = "Required Approvers",
            ["value"] = request.RequiredApprovers.ToString(),
        });

        facts.Add(new JsonObject
        {
            ["title"] = "Timeout",
            ["value"] = request.Timeout.ToString(),
        });

        facts.Add(new JsonObject
        {
            ["title"] = "Correlation ID",
            ["value"] = request.CorrelationId,
        });

        body.Add(new JsonObject
        {
            ["type"] = "FactSet",
            ["facts"] = facts,
        });

        // Actions
        var actions = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "Action.Submit",
                ["title"] = "Approve",
                ["data"] = new JsonObject
                {
                    ["correlationId"] = request.CorrelationId,
                    ["decision"] = "approve",
                    ["token"] = approveActionToken,
                },
            },
            new JsonObject
            {
                ["type"] = "Action.Submit",
                ["title"] = "Reject",
                ["data"] = new JsonObject
                {
                    ["correlationId"] = request.CorrelationId,
                    ["decision"] = "reject",
                    ["token"] = rejectActionToken,
                },
            },
        };

        return new JsonObject
        {
            ["type"] = "AdaptiveCard",
            ["version"] = CardVersion,
            ["$schema"] = CardSchema,
            ["body"] = body,
            ["actions"] = actions,
        };
    }

    /// <summary>
    /// Wraps an Adaptive Card in the Teams incoming-webhook message envelope.
    /// </summary>
    /// <param name="card">The Adaptive Card to wrap.</param>
    /// <returns>
    /// A <see cref="JsonObject"/> conforming to the Teams message attachment schema.
    /// </returns>
    public static JsonObject BuildMessageEnvelope(JsonObject card)
    {
        if (card is null) throw new ArgumentNullException(nameof(card));

        return new JsonObject
        {
            ["type"] = "message",
            ["attachments"] = new JsonArray
            {
                new JsonObject
                {
                    ["contentType"] = "application/vnd.microsoft.card.adaptive",
                    ["content"] = card,
                },
            },
        };
    }
}
