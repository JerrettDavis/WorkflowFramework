using FluentAssertions;
using WorkflowFramework.Extensions.Approvals.Email;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Email.Tests;

public sealed class EmailMessageBuilderTests
{
    private static EmailApprovalOptions BaseOptions() => new()
    {
        SmtpHost = "smtp.test",
        From = "from@test.com",
        ApproveUrlTemplate = "https://example.com/approve?t={token}",
        RejectUrlTemplate = "https://example.com/reject?t={token}",
        TokenSigningKey = Convert.ToBase64String(new byte[32])
    };

    // ------------------------------------------------------------------
    // Build substitutes all placeholders
    // ------------------------------------------------------------------

    [Fact]
    public void Build_SubstitutesAllPlaceholders()
    {
        var opts = BaseOptions();
        var msg = EmailMessageBuilder.Build(opts, "alice@example.com", "My Title", "My Description",
            "https://approve.url", "https://reject.url");

        msg.Subject.Should().Contain("My Title");
        msg.Body.Should().Contain("My Title");
        msg.Body.Should().Contain("My Description");
        msg.Body.Should().Contain("https://approve.url");
        msg.Body.Should().Contain("https://reject.url");
    }

    [Fact]
    public void Build_NullDescription_DoesNotThrow()
    {
        var opts = BaseOptions();
        var act = () => EmailMessageBuilder.Build(opts, "alice@example.com", "Title", null,
            "https://approve", "https://reject");

        act.Should().NotThrow();
    }

    [Fact]
    public void Build_UsesFrom()
    {
        var opts = BaseOptions();
        opts.From = "sender@company.com";

        var msg = EmailMessageBuilder.Build(opts, "recipient@example.com", "T", null, "a", "r");

        msg.From.Should().Be("sender@company.com");
    }

    [Fact]
    public void Build_SetsToRecipient()
    {
        var opts = BaseOptions();
        var msg = EmailMessageBuilder.Build(opts, "bob@example.com", "T", null, "a", "r");

        msg.To.Should().ContainSingle(r => r == "bob@example.com");
    }

    [Fact]
    public void Build_IsHtml_True()
    {
        var opts = BaseOptions();
        var msg = EmailMessageBuilder.Build(opts, "x@y.com", "T", null, "a", "r");

        msg.IsHtml.Should().BeTrue();
    }

    [Fact]
    public void Build_CustomBodyTemplate_UsesIt()
    {
        var opts = BaseOptions();
        opts.BodyTemplate = "Custom: {title} | {approveUrl} | {rejectUrl}";

        var msg = EmailMessageBuilder.Build(opts, "x@y.com", "MyTitle", null, "https://approve", "https://reject");

        msg.Body.Should().Be("Custom: MyTitle | https://approve | https://reject");
    }

    [Fact]
    public void Build_CustomSubjectTemplate_UsesIt()
    {
        var opts = BaseOptions();
        opts.SubjectTemplate = "Please review: {title}";

        var msg = EmailMessageBuilder.Build(opts, "x@y.com", "Deployment", null, "a", "r");

        msg.Subject.Should().Be("Please review: Deployment");
    }

    // ------------------------------------------------------------------
    // ResolveRecipients
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveRecipients_FromStringArray_ReturnsAll()
    {
        var context = new Dictionary<string, object?>
        {
            ["recipients"] = new[] { "a@b.com", "c@d.com" }
        };

        var result = EmailMessageBuilder.ResolveRecipients(context, "recipients");

        result.Should().BeEquivalentTo(new[] { "a@b.com", "c@d.com" });
    }

    [Fact]
    public void ResolveRecipients_FromIEnumerable_ReturnsAll()
    {
        var context = new Dictionary<string, object?>
        {
            ["recipients"] = (IEnumerable<string>)new List<string> { "a@b.com", "c@d.com" }
        };

        var result = EmailMessageBuilder.ResolveRecipients(context, "recipients");

        result.Should().BeEquivalentTo(new[] { "a@b.com", "c@d.com" });
    }

    [Fact]
    public void ResolveRecipients_FromCommaSeparatedString_SplitsCorrectly()
    {
        var context = new Dictionary<string, object?>
        {
            ["recipients"] = "a@b.com, c@d.com, e@f.com"
        };

        var result = EmailMessageBuilder.ResolveRecipients(context, "recipients");

        result.Should().BeEquivalentTo(new[] { "a@b.com", "c@d.com", "e@f.com" });
    }

    [Fact]
    public void ResolveRecipients_MissingKey_ReturnsEmpty()
    {
        var context = new Dictionary<string, object?>();

        var result = EmailMessageBuilder.ResolveRecipients(context, "recipients");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveRecipients_NullValue_ReturnsEmpty()
    {
        var context = new Dictionary<string, object?> { ["recipients"] = null };

        var result = EmailMessageBuilder.ResolveRecipients(context, "recipients");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveRecipients_FiltersBlankEntries()
    {
        var context = new Dictionary<string, object?>
        {
            ["recipients"] = " , a@b.com,  , c@d.com"
        };

        var result = EmailMessageBuilder.ResolveRecipients(context, "recipients");

        result.Should().BeEquivalentTo(new[] { "a@b.com", "c@d.com" });
    }
}
