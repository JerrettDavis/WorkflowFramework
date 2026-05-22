namespace WorkflowFramework.Extensions.Approvals.Teams;

/// <summary>
/// Specifies the delivery mode used by the Teams approval channel.
/// </summary>
public enum TeamsApprovalMode
{
    /// <summary>
    /// Posts messages to a channel via an Office 365 Incoming Webhook connector URL.
    /// This mode is send-only: the card is displayed to users but Teams cannot route
    /// <c>Action.Submit</c> button events back to an arbitrary HTTPS endpoint via the
    /// incoming-webhook connector. To collect button responses you must use
    /// <see cref="Bot"/> mode or a Power Automate flow.
    /// </summary>
    IncomingWebhook,

    /// <summary>
    /// Sends and receives messages through the Bot Framework conversation API.
    /// <para>
    /// <b>Placeholder / minimal implementation:</b> the channel posts to
    /// <c>{BotServiceUrl}/v3/conversations/{BotConversationId}/activities</c> using the
    /// configured password as a bearer token.  A production deployment requires a full
    /// Azure AD client-credentials token acquisition flow (client_id + client_secret against
    /// <c>https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token</c>).  This
    /// implementation does <em>not</em> perform that exchange; it is provided as a structural
    /// starting point only.
    /// </para>
    /// </summary>
    Bot,
}
