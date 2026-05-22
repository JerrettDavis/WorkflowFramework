using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// Default <see cref="IEmailSender"/> implementation backed by <see cref="SmtpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SmtpClient"/> is documented by Microsoft as obsolete and is not recommended for
/// new cross-platform applications. For production use, consider replacing this implementation
/// with a modern email SDK (e.g., MailKit, SendGrid) by registering your own
/// <see cref="IEmailSender"/> in the DI container before calling <c>UseEmail</c>.
/// </para>
/// <para>
/// Authentication is performed with a <see cref="NetworkCredential"/> when both
/// <see cref="EmailApprovalOptions.Username"/> and <see cref="EmailApprovalOptions.Password"/>
/// are configured; otherwise the SMTP client connects without credentials.
/// </para>
/// </remarks>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailApprovalOptions _options;

    /// <summary>
    /// Initialises a new instance of <see cref="SmtpEmailSender"/>.
    /// </summary>
    /// <param name="options">The email approval options. Must not be <see langword="null"/>.</param>
    public SmtpEmailSender(IOptions<EmailApprovalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var client = BuildClient();
        using var mail = BuildMailMessage(message);

        await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
    }

    private SmtpClient BuildClient()
    {
        var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);

        return client;
    }

    /// <summary>
    /// Converts an <see cref="EmailMessage"/> record to a <see cref="MailMessage"/> for use with <see cref="SmtpClient"/>.
    /// Exposed for testability.
    /// </summary>
    public static MailMessage BuildMailMessage(EmailMessage message)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(message.From),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsHtml
        };

        foreach (var to in message.To)
            mail.To.Add(to);

        return mail;
    }
}
