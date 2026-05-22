namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// Pure-function helper that constructs <see cref="EmailMessage"/> instances from approval
/// request data and rendered token URLs. Extracted from <see cref="EmailApprovalChannel"/>
/// so the message-construction logic can be tested without an SMTP server.
/// </summary>
public static class EmailMessageBuilder
{
    private const string DefaultBodyTemplate =
        """
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Approval Request</title></head>
        <body>
        <h2>{title}</h2>
        <p>{description}</p>
        <p>
          <a href="{approveUrl}" style="padding:8px 16px;background:#28a745;color:#fff;text-decoration:none;border-radius:4px;">Approve</a>
          &nbsp;&nbsp;
          <a href="{rejectUrl}" style="padding:8px 16px;background:#dc3545;color:#fff;text-decoration:none;border-radius:4px;">Reject</a>
        </p>
        </body>
        </html>
        """;

    /// <summary>
    /// Builds an <see cref="EmailMessage"/> for a single recipient, substituting all known
    /// placeholders in the subject and body templates.
    /// </summary>
    /// <param name="options">The configured email options.</param>
    /// <param name="recipient">The recipient email address.</param>
    /// <param name="title">The approval request title.</param>
    /// <param name="description">The approval request description (may be <see langword="null"/>).</param>
    /// <param name="approveUrl">The fully resolved approve callback URL.</param>
    /// <param name="rejectUrl">The fully resolved reject callback URL.</param>
    /// <returns>A populated <see cref="EmailMessage"/> ready for dispatch.</returns>
    public static EmailMessage Build(
        EmailApprovalOptions options,
        string recipient,
        string title,
        string? description,
        string approveUrl,
        string rejectUrl)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(approveUrl);
        ArgumentNullException.ThrowIfNull(rejectUrl);

        var subject = options.SubjectTemplate
            .Replace("{title}", title, StringComparison.OrdinalIgnoreCase);

        var bodyTemplate = options.BodyTemplate ?? DefaultBodyTemplate;
        var body = bodyTemplate
            .Replace("{title}", title, StringComparison.OrdinalIgnoreCase)
            .Replace("{description}", description ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{approveUrl}", approveUrl, StringComparison.OrdinalIgnoreCase)
            .Replace("{rejectUrl}", rejectUrl, StringComparison.OrdinalIgnoreCase);

        return new EmailMessage(
            From: options.From,
            To: [recipient],
            Subject: subject,
            Body: body,
            IsHtml: true);
    }

    /// <summary>
    /// Resolves the list of recipient email addresses from the approval request context.
    /// Accepts <c>IEnumerable&lt;string&gt;</c>, <c>string[]</c>, or a single
    /// comma-separated <see cref="string"/>.
    /// </summary>
    /// <param name="context">The approval request context dictionary.</param>
    /// <param name="key">The context key to look up.</param>
    /// <returns>A non-empty list of recipient addresses, or an empty list when none are found.</returns>
    public static IReadOnlyList<string> ResolveRecipients(
        IReadOnlyDictionary<string, object?> context,
        string key)
    {
        if (!context.TryGetValue(key, out var raw) || raw is null)
            return [];

        return raw switch
        {
            IEnumerable<string> enumerable => enumerable
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList()
                .AsReadOnly(),

            string s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList()
                .AsReadOnly(),

            _ => []
        };
    }
}
