using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Approvals.Slack;
using WorkflowFramework.Extensions.Approvals.Slack.DependencyInjection;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Slack.Tests;

public sealed class SlackApprovalsBuilderExtensionsTests
{
    [Fact]
    public void UseSlack_RegistersSlackApprovalChannel()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseSlack(opts =>
        {
            opts.BotToken = "xoxb-test";
            opts.ChannelId = "C123";
            opts.SigningSecret = "secret";
        });

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(SlackApprovalChannel));
    }

    [Fact]
    public void UseSlack_RegistersSlackApprovalChannelAsIApprovalChannel()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseSlack(opts =>
        {
            opts.BotToken = "xoxb-test";
            opts.ChannelId = "C123";
            opts.SigningSecret = "secret";
        });

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IApprovalChannel) &&
            sd.ImplementationType == typeof(SlackApprovalChannel));
    }

    [Fact]
    public void UseSlack_RegistersOptionsValidator()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseSlack(opts =>
        {
            opts.BotToken = "xoxb-test";
            opts.ChannelId = "C123";
            opts.SigningSecret = "secret";
        });

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IValidateOptions<SlackApprovalOptions>) &&
            sd.ImplementationType == typeof(SlackApprovalOptionsValidator));
    }

    [Fact]
    public void UseSlack_RegistersSignatureValidator()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseSlack(opts =>
        {
            opts.BotToken = "xoxb-test";
            opts.ChannelId = "C123";
            opts.SigningSecret = "secret";
        });

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(SlackSignatureValidator));
    }

    [Fact]
    public void UseSlack_RegistersInteractivityHandler()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseSlack(opts =>
        {
            opts.BotToken = "xoxb-test";
            opts.ChannelId = "C123";
            opts.SigningSecret = "secret";
        });

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(SlackInteractivityHandler));
    }

    [Fact]
    public void UseSlack_RegistersNamedHttpClient()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseSlack(opts =>
        {
            opts.BotToken = "xoxb-test";
            opts.ChannelId = "C123";
            opts.SigningSecret = "secret";
        });

        // Named HTTP client registers IHttpClientFactory
        services.Should().Contain(sd => sd.ServiceType == typeof(System.Net.Http.IHttpClientFactory));
    }

    [Fact]
    public void UseSlack_ConfiguresDelegateIsApplied()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseSlack(opts =>
        {
            opts.BotToken = "xoxb-custom";
            opts.ChannelId = "CCUSTOM";
            opts.SigningSecret = "customsecret";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SlackApprovalOptions>>();

        options.Value.BotToken.Should().Be("xoxb-custom");
        options.Value.ChannelId.Should().Be("CCUSTOM");
        options.Value.SigningSecret.Should().Be("customsecret");
    }

    [Fact]
    public void UseSlack_ThrowsOnNullBuilder()
    {
        IApprovalsBuilder? builder = null;
        var act = () => builder!.UseSlack(_ => { });
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void UseSlack_ThrowsOnNullConfigure()
    {
        var services = new ServiceCollection();
        var builder = services.AddApprovals();

        var act = () => builder.UseSlack(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }
}
