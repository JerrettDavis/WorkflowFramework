namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// Abstraction over an email delivery mechanism. Replace the default
/// <see cref="SmtpEmailSender"/> implementation with a custom one (e.g., SendGrid,
/// Mailgun) by registering it in DI before calling <c>UseEmail</c>.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends the given <paramref name="message"/> asynchronously.
    /// </summary>
    /// <param name="message">The email message to send. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>A <see cref="Task"/> that completes when the message has been handed off to the mail server.</returns>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// An immutable description of an email message to be sent via <see cref="IEmailSender"/>.
/// </summary>
/// <param name="From">The sender address.</param>
/// <param name="To">The ordered list of recipient addresses. Must contain at least one entry.</param>
/// <param name="Subject">The email subject line.</param>
/// <param name="Body">The email body content.</param>
/// <param name="IsHtml">
/// <see langword="true"/> when <paramref name="Body"/> contains HTML markup;
/// <see langword="false"/> for plain-text bodies.
/// </param>
public sealed record EmailMessage(
    string From,
    IReadOnlyList<string> To,
    string Subject,
    string Body,
    bool IsHtml);
