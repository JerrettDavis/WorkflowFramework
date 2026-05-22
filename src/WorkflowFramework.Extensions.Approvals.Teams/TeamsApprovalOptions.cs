namespace WorkflowFramework.Extensions.Approvals.Teams;

/// <summary>
/// Configuration options for the Microsoft Teams approval channel.
/// Validated at startup via <see cref="TeamsApprovalOptionsValidator"/>.
/// </summary>
public sealed class TeamsApprovalOptions
{
    /// <summary>
    /// Gets or sets the delivery mode. Defaults to <see cref="TeamsApprovalMode.IncomingWebhook"/>.
    /// </summary>
    public TeamsApprovalMode Mode { get; set; } = TeamsApprovalMode.IncomingWebhook;

    // -------------------------------------------------------------------------
    // IncomingWebhook mode
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the Office 365 Incoming Webhook connector URL.
    /// Required when <see cref="Mode"/> is <see cref="TeamsApprovalMode.IncomingWebhook"/>.
    /// Must be a valid HTTPS URI.
    /// </summary>
    /// <remarks>
    /// <b>Send-only limitation:</b> Incoming Webhook connectors cannot receive
    /// <c>Action.Submit</c> button callbacks from Teams Adaptive Cards.  Button
    /// events from cards posted via this mode are not routed back to the
    /// <see cref="CallbackPath"/> endpoint.  Use <see cref="TeamsApprovalMode.Bot"/>
    /// if you need two-way interaction.
    /// </remarks>
    public string? WebhookUrl { get; set; }

    // -------------------------------------------------------------------------
    // Bot mode
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the Bot Framework service base URL (e.g. <c>https://smba.trafficmanager.net/apis</c>).
    /// Required when <see cref="Mode"/> is <see cref="TeamsApprovalMode.Bot"/>.
    /// </summary>
    public string? BotServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets the target Bot Framework conversation ID.
    /// Required when <see cref="Mode"/> is <see cref="TeamsApprovalMode.Bot"/>.
    /// </summary>
    public string? BotConversationId { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD Application (client) ID for the bot registration.
    /// Required when <see cref="Mode"/> is <see cref="TeamsApprovalMode.Bot"/>.
    /// </summary>
    public string? BotAppId { get; set; }

    /// <summary>
    /// Gets or sets the bot application password / client secret.
    /// Required when <see cref="Mode"/> is <see cref="TeamsApprovalMode.Bot"/>.
    /// <para>
    /// <b>Placeholder note:</b> this value is currently used as a bearer token directly.
    /// A production implementation must exchange <see cref="BotAppId"/> and this secret
    /// for an OAuth 2.0 access token via the Azure AD client-credentials flow before
    /// calling the Bot Framework API.
    /// </para>
    /// </summary>
    public string? BotAppPassword { get; set; }

    // -------------------------------------------------------------------------
    // Shared
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the ASP.NET Core route path for the Teams callback endpoint.
    /// Defaults to <c>/approvals/teams/callback</c>.
    /// </summary>
    public string CallbackPath { get; set; } = "/approvals/teams/callback";

    /// <summary>
    /// Gets or sets the named <see cref="System.Net.Http.HttpClient"/> used to post
    /// messages to Teams.  Defaults to <c>"approvals.teams"</c>.
    /// </summary>
    public string HttpClientName { get; set; } = "approvals.teams";

    /// <summary>
    /// Gets or sets the shared secret used to HMAC-sign the action data tokens
    /// embedded in Adaptive Card buttons.  Must be at least 16 characters.
    /// </summary>
    /// <remarks>
    /// Because Teams <c>Action.Submit</c> posts arbitrary JSON back to the configured
    /// endpoint without a built-in request-signing mechanism, the channel embeds a
    /// short-lived HMAC-SHA256 token in each button's action data.  The callback
    /// endpoint verifies this token to confirm the payload originated from a card
    /// produced by this service.
    /// </remarks>
    public string CallbackSharedSecret { get; set; } = string.Empty;
}
