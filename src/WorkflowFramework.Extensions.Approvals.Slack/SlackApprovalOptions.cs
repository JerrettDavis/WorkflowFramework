namespace WorkflowFramework.Extensions.Approvals.Slack;

/// <summary>
/// Configuration options for the Slack approval channel.
/// Validated at startup via <see cref="SlackApprovalOptionsValidator"/>.
/// </summary>
public sealed class SlackApprovalOptions
{
    /// <summary>
    /// Gets or sets the Slack Bot Token used for API calls.
    /// Must start with <c>xoxb-</c>. Required.
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Slack channel ID where approval messages are posted.
    /// Example: <c>C12345</c>. Required.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Slack signing secret used to verify interaction payloads.
    /// Required.
    /// </summary>
    public string SigningSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL for the Slack API.
    /// Defaults to <c>https://slack.com/api/</c>.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://slack.com/api/";

    /// <summary>
    /// Gets or sets the ASP.NET Core route path for the Slack interactivity endpoint.
    /// Defaults to <c>/approvals/slack/interact</c>.
    /// </summary>
    public string CallbackPath { get; set; } = "/approvals/slack/interact";

    /// <summary>
    /// Gets or sets the maximum age in seconds for a Slack request signature.
    /// Requests older than this value are rejected to prevent replay attacks.
    /// Defaults to <c>300</c> (5 minutes).
    /// </summary>
    public int RequestSignatureMaxAgeSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the named <see cref="System.Net.Http.HttpClient"/> used for Slack API calls.
    /// Defaults to <c>approvals.slack</c>.
    /// </summary>
    public string HttpClientName { get; set; } = "approvals.slack";
}
