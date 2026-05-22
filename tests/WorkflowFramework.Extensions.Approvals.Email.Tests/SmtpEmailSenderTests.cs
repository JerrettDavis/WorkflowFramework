using FluentAssertions;
using Microsoft.Extensions.Options;
using WorkflowFramework.Extensions.Approvals.Email;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Email.Tests;

public sealed class SmtpEmailSenderTests
{
    private static SmtpEmailSender CreateSender(Action<EmailApprovalOptions>? configure = null)
    {
        var opts = new EmailApprovalOptions
        {
            SmtpHost = "localhost",
            SmtpPort = 25,
            From = "test@test.com",
            ApproveUrlTemplate = "https://example.com/approve?t={token}",
            RejectUrlTemplate = "https://example.com/reject?t={token}",
            TokenSigningKey = Convert.ToBase64String(new byte[32])
        };
        configure?.Invoke(opts);
        return new SmtpEmailSender(Options.Create(opts));
    }

    // ------------------------------------------------------------------
    // Constructor accepts valid options
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_ValidOptions_DoesNotThrow()
    {
        var act = () => CreateSender();
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new SmtpEmailSender(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------
    // BuildMailMessage constructs correct MailMessage (via the static internal helper)
    // ------------------------------------------------------------------

    [Fact]
    public void BuildMailMessage_SetsCorrectFrom()
    {
        var message = new EmailMessage("from@test.com", ["to@test.com"], "Subject", "Body", false);
        var mail = SmtpEmailSender.BuildMailMessage(message);

        mail.From!.Address.Should().Be("from@test.com");
    }

    [Fact]
    public void BuildMailMessage_SetsCorrectTo()
    {
        var message = new EmailMessage("from@test.com", ["a@b.com", "c@d.com"], "Subject", "Body", false);
        var mail = SmtpEmailSender.BuildMailMessage(message);

        mail.To.Select(t => t.Address).Should().BeEquivalentTo(new[] { "a@b.com", "c@d.com" });
    }

    [Fact]
    public void BuildMailMessage_SetsSubjectAndBody()
    {
        var message = new EmailMessage("from@test.com", ["to@test.com"], "Hello Subject", "Hello Body", false);
        var mail = SmtpEmailSender.BuildMailMessage(message);

        mail.Subject.Should().Be("Hello Subject");
        mail.Body.Should().Be("Hello Body");
    }

    [Fact]
    public void BuildMailMessage_IsHtmlTrue_SetsBodyHtml()
    {
        var message = new EmailMessage("from@test.com", ["to@test.com"], "S", "<b>Body</b>", IsHtml: true);
        var mail = SmtpEmailSender.BuildMailMessage(message);

        mail.IsBodyHtml.Should().BeTrue();
    }

    [Fact]
    public void BuildMailMessage_IsHtmlFalse_SetsPlainText()
    {
        var message = new EmailMessage("from@test.com", ["to@test.com"], "S", "Plain", IsHtml: false);
        var mail = SmtpEmailSender.BuildMailMessage(message);

        mail.IsBodyHtml.Should().BeFalse();
    }
}
