using System.Text.Json.Nodes;

namespace WorkflowFramework.Extensions.Approvals.Slack.Blocks;

/// <summary>
/// Builds Slack Block Kit JSON payloads for approval messages sent via <c>chat.postMessage</c>
/// and for updated messages after a vote has been cast.
/// </summary>
public static class SlackBlockKitBuilder
{
    // Slack header text limit is 150 characters.
    private const int SlackHeaderMaxLength = 150;

    /// <summary>
    /// Builds the Block Kit JSON payload for <c>chat.postMessage</c> representing an initial
    /// approval request. The message includes a header, description, metadata fields, optional
    /// context fields, and Approve/Reject action buttons.
    /// </summary>
    /// <param name="channelId">The Slack channel ID to post to.</param>
    /// <param name="request">The approval request to render.</param>
    /// <returns>A <see cref="JsonObject"/> ready to serialize and POST to the Slack API.</returns>
    public static JsonObject BuildApprovalMessage(string channelId, ApprovalRequest request)
    {
        if (channelId is null) throw new ArgumentNullException(nameof(channelId));
        if (request is null) throw new ArgumentNullException(nameof(request));

        var title = Truncate(request.Title, SlackHeaderMaxLength);
        var correlationId = request.CorrelationId;

        var blocks = new JsonArray();

        // Header block
        blocks.Add(new JsonObject
        {
            ["type"] = "header",
            ["text"] = new JsonObject
            {
                ["type"] = "plain_text",
                ["text"] = title,
                ["emoji"] = true
            }
        });

        // Description section
        if (!string.IsNullOrEmpty(request.Description))
        {
            blocks.Add(new JsonObject
            {
                ["type"] = "section",
                ["text"] = new JsonObject
                {
                    ["type"] = "mrkdwn",
                    ["text"] = request.Description
                }
            });
        }

        // Metadata fields
        var metaFields = new JsonArray
        {
            MrkdwnField($"*Required approvers:*\n{request.RequiredApprovers}"),
            MrkdwnField($"*Timeout:*\n{FormatTimeout(request.Timeout)}"),
            MrkdwnField($"*Correlation ID:*\n`{correlationId}`")
        };

        blocks.Add(new JsonObject
        {
            ["type"] = "section",
            ["fields"] = metaFields
        });

        // Optional context fields
        if (request.Context is { Count: > 0 })
        {
            var contextFields = new JsonArray();
            foreach (var kv in request.Context)
            {
                var value = kv.Value?.ToString() ?? string.Empty;
                contextFields.Add(MrkdwnField($"*{kv.Key}:*\n{value}"));
            }

            blocks.Add(new JsonObject
            {
                ["type"] = "section",
                ["fields"] = contextFields
            });
        }

        // Divider
        blocks.Add(new JsonObject { ["type"] = "divider" });

        // Action buttons
        blocks.Add(new JsonObject
        {
            ["type"] = "actions",
            ["elements"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "button",
                    ["text"] = new JsonObject
                    {
                        ["type"] = "plain_text",
                        ["text"] = "Approve",
                        ["emoji"] = true
                    },
                    ["style"] = "primary",
                    ["action_id"] = $"approve:{correlationId}",
                    ["value"] = correlationId
                },
                new JsonObject
                {
                    ["type"] = "button",
                    ["text"] = new JsonObject
                    {
                        ["type"] = "plain_text",
                        ["text"] = "Reject",
                        ["emoji"] = true
                    },
                    ["style"] = "danger",
                    ["action_id"] = $"reject:{correlationId}",
                    ["value"] = correlationId
                }
            }
        });

        return new JsonObject
        {
            ["channel"] = channelId,
            ["text"] = title,
            ["blocks"] = blocks
        };
    }

    /// <summary>
    /// Builds an updated Block Kit message payload reflecting the outcome of a vote.
    /// Replaces the action buttons with a status section.
    /// </summary>
    /// <param name="request">The original approval request.</param>
    /// <param name="vote">The vote that was cast.</param>
    /// <returns>A <see cref="JsonObject"/> suitable for use with <c>chat.update</c>.</returns>
    public static JsonObject BuildUpdatedMessage(ApprovalRequest request, ApprovalRecord vote)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (vote is null) throw new ArgumentNullException(nameof(vote));

        var title = Truncate(request.Title, SlackHeaderMaxLength);
        var status = vote.Approved ? "Approved" : "Rejected";
        var approverName = vote.ApproverDisplayName ?? vote.ApproverId;

        var blocks = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "header",
                ["text"] = new JsonObject
                {
                    ["type"] = "plain_text",
                    ["text"] = title,
                    ["emoji"] = true
                }
            },
            new JsonObject
            {
                ["type"] = "section",
                ["text"] = new JsonObject
                {
                    ["type"] = "mrkdwn",
                    ["text"] = $"*Status:* {status} by {approverName}"
                }
            }
        };

        return new JsonObject
        {
            ["text"] = title,
            ["blocks"] = blocks
        };
    }

    private static JsonObject MrkdwnField(string text) =>
        new JsonObject
        {
            ["type"] = "mrkdwn",
            ["text"] = text
        };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string FormatTimeout(TimeSpan timeout)
    {
        if (timeout.TotalDays >= 1)
            return $"{timeout.TotalDays:0.#} day(s)";
        if (timeout.TotalHours >= 1)
            return $"{timeout.TotalHours:0.#} hour(s)";
        if (timeout.TotalMinutes >= 1)
            return $"{timeout.TotalMinutes:0.#} minute(s)";
        return $"{timeout.TotalSeconds:0.#} second(s)";
    }
}
