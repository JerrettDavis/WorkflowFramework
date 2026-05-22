using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Teams.Tests;

public sealed class TeamsApprovalOptionsValidatorTests
{
    private static readonly TeamsApprovalOptionsValidator Validator = new();

    private static ValidateOptionsResult Validate(TeamsApprovalOptions options) =>
        Validator.Validate(null, options);

    // ------------------------------------------------------------------
    // IncomingWebhook mode
    // ------------------------------------------------------------------

    [Fact]
    public void IncomingWebhook_ValidWebhookUrl_Passes()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.IncomingWebhook,
            WebhookUrl = "https://outlook.office.com/webhook/test",
            CallbackSharedSecret = "1234567890abcdef",
        };

        Validate(options).Failed.Should().BeFalse();
    }

    [Fact]
    public void IncomingWebhook_MissingWebhookUrl_Fails()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.IncomingWebhook,
            WebhookUrl = null,
            CallbackSharedSecret = "1234567890abcdef",
        };

        var result = Validate(options);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(TeamsApprovalOptions.WebhookUrl));
    }

    [Fact]
    public void IncomingWebhook_HttpWebhookUrl_Fails()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.IncomingWebhook,
            WebhookUrl = "http://not-https.example.com/webhook",
            CallbackSharedSecret = "1234567890abcdef",
        };

        var result = Validate(options);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("HTTPS");
    }

    [Fact]
    public void IncomingWebhook_InvalidUri_Fails()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.IncomingWebhook,
            WebhookUrl = "not a uri at all !!",
            CallbackSharedSecret = "1234567890abcdef",
        };

        var result = Validate(options);
        result.Failed.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Bot mode
    // ------------------------------------------------------------------

    [Fact]
    public void Bot_AllFieldsPresent_Passes()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.Bot,
            BotServiceUrl = "https://smba.trafficmanager.net/apis",
            BotConversationId = "19:abc@thread.tacv2",
            BotAppId = "app-id-guid",
            BotAppPassword = "super-secret-password",
            CallbackSharedSecret = "1234567890abcdef",
        };

        Validate(options).Failed.Should().BeFalse();
    }

    [Fact]
    public void Bot_MissingBotServiceUrl_Fails()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.Bot,
            BotConversationId = "conv",
            BotAppId = "app",
            BotAppPassword = "pass",
            CallbackSharedSecret = "1234567890abcdef",
        };

        var result = Validate(options);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(TeamsApprovalOptions.BotServiceUrl));
    }

    [Fact]
    public void Bot_MissingBotConversationId_Fails()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.Bot,
            BotServiceUrl = "https://smba.trafficmanager.net/apis",
            BotAppId = "app",
            BotAppPassword = "pass",
            CallbackSharedSecret = "1234567890abcdef",
        };

        var result = Validate(options);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(TeamsApprovalOptions.BotConversationId));
    }

    [Fact]
    public void Bot_MissingBotAppId_Fails()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.Bot,
            BotServiceUrl = "https://smba.trafficmanager.net/apis",
            BotConversationId = "conv",
            BotAppPassword = "pass",
            CallbackSharedSecret = "1234567890abcdef",
        };

        var result = Validate(options);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(TeamsApprovalOptions.BotAppId));
    }

    [Fact]
    public void Bot_MissingBotAppPassword_Fails()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.Bot,
            BotServiceUrl = "https://smba.trafficmanager.net/apis",
            BotConversationId = "conv",
            BotAppId = "app",
            CallbackSharedSecret = "1234567890abcdef",
        };

        var result = Validate(options);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(TeamsApprovalOptions.BotAppPassword));
    }

    // ------------------------------------------------------------------
    // CallbackSharedSecret
    // ------------------------------------------------------------------

    [Fact]
    public void MissingCallbackSharedSecret_Fails()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.IncomingWebhook,
            WebhookUrl = "https://outlook.office.com/webhook/test",
            CallbackSharedSecret = "",
        };

        var result = Validate(options);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(TeamsApprovalOptions.CallbackSharedSecret));
    }

    [Fact]
    public void ShortCallbackSharedSecret_Fails()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.IncomingWebhook,
            WebhookUrl = "https://outlook.office.com/webhook/test",
            CallbackSharedSecret = "short",
        };

        var result = Validate(options);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(TeamsApprovalOptions.CallbackSharedSecret));
    }

    [Fact]
    public void ExactlyMinimumLengthCallbackSharedSecret_Passes()
    {
        var options = new TeamsApprovalOptions
        {
            Mode = TeamsApprovalMode.IncomingWebhook,
            WebhookUrl = "https://outlook.office.com/webhook/test",
            CallbackSharedSecret = "1234567890123456", // exactly 16
        };

        Validate(options).Failed.Should().BeFalse();
    }
}
