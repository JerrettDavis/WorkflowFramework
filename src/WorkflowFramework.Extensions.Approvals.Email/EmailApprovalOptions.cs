namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// Configuration options for the email approval channel.
/// Validated at startup via <see cref="EmailApprovalOptionsValidator"/>.
/// </summary>
public sealed class EmailApprovalOptions
{
    /// <summary>Gets or sets the SMTP host name or IP address. Required.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP port. Defaults to 25.</summary>
    public int SmtpPort { get; set; } = 25;

    /// <summary>Gets or sets a value indicating whether SSL/TLS is used for the SMTP connection. Defaults to <see langword="false"/>.</summary>
    public bool EnableSsl { get; set; }

    /// <summary>Gets or sets the SMTP username for authentication. When both <see cref="Username"/> and <see cref="Password"/> are set, a <see cref="System.Net.NetworkCredential"/> is supplied to the SMTP client.</summary>
    public string? Username { get; set; }

    /// <summary>Gets or sets the SMTP password for authentication.</summary>
    public string? Password { get; set; }

    /// <summary>Gets or sets the sender email address used in the <c>From</c> header. Required.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email subject template.
    /// Supports the placeholder <c>{title}</c> which is replaced at send time.
    /// Defaults to <c>"Approval requested: {title}"</c>.
    /// </summary>
    public string SubjectTemplate { get; set; } = "Approval requested: {title}";

    /// <summary>
    /// Gets or sets the email body template.
    /// Supports placeholders: <c>{title}</c>, <c>{description}</c>, <c>{approveUrl}</c>, <c>{rejectUrl}</c>.
    /// When <see langword="null"/>, a sensible HTML default is used.
    /// </summary>
    public string? BodyTemplate { get; set; }

    /// <summary>
    /// Gets or sets the approve callback URL template. Must contain the placeholder <c>{token}</c>.
    /// Example: <c>https://example.com/approvals/email/respond?t={token}</c>. Required.
    /// </summary>
    public string ApproveUrlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reject callback URL template. Must contain the placeholder <c>{token}</c>.
    /// Example: <c>https://example.com/approvals/email/respond?t={token}</c>. Required.
    /// </summary>
    public string RejectUrlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base64-encoded HMAC signing key. Must decode to at least 32 raw bytes. Required.
    /// </summary>
    public string TokenSigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lifetime of generated tokens. When <see langword="null"/>, the token lifetime
    /// matches the approval request's <see cref="ApprovalRequest.Timeout"/>.
    /// </summary>
    public TimeSpan? TokenLifetime { get; set; }

    /// <summary>
    /// Gets or sets the ASP.NET Core route path for the email approval callback endpoint.
    /// Defaults to <c>/approvals/email/respond</c>.
    /// </summary>
    public string CallbackPath { get; set; } = "/approvals/email/respond";

    /// <summary>
    /// Gets or sets the key used to look up the list of recipient email addresses in
    /// <see cref="ApprovalRequest.Context"/>. Defaults to <c>"recipients"</c>.
    /// </summary>
    public string RecipientsContextKey { get; set; } = "recipients";
}
