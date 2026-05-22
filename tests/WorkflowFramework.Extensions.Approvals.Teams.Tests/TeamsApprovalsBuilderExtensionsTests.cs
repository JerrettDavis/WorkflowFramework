using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Approvals.Teams.DependencyInjection;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Teams.Tests;

public sealed class TeamsApprovalsBuilderExtensionsTests
{
    private static IServiceCollection BuildServices(Action<TeamsApprovalOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddApprovals()
            .UseTeams(opts =>
            {
                opts.Mode = TeamsApprovalMode.IncomingWebhook;
                opts.WebhookUrl = "https://outlook.office.com/webhook/test";
                opts.CallbackSharedSecret = "1234567890abcdef";
                configure?.Invoke(opts);
            });
        return services;
    }

    // ------------------------------------------------------------------
    // Service registrations
    // ------------------------------------------------------------------

    [Fact]
    public void UseTeams_RegistersTeamsApprovalOptions()
    {
        var services = BuildServices();
        services.Should().Contain(sd => sd.ServiceType == typeof(IConfigureOptions<TeamsApprovalOptions>));
    }

    [Fact]
    public void UseTeams_RegistersOptionsValidator()
    {
        var services = BuildServices();
        services.Should().Contain(sd => sd.ServiceType == typeof(IValidateOptions<TeamsApprovalOptions>));
    }

    [Fact]
    public void UseTeams_RegistersTeamsCallbackTokenService()
    {
        var services = BuildServices();
        services.Should().Contain(sd => sd.ServiceType == typeof(TeamsCallbackTokenService));
    }

    [Fact]
    public void UseTeams_RegistersTeamsCallbackHandler()
    {
        var services = BuildServices();
        services.Should().Contain(sd => sd.ServiceType == typeof(TeamsCallbackHandler));
    }

    [Fact]
    public void UseTeams_RegistersTeamsApprovalChannel()
    {
        var services = BuildServices();
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IApprovalChannel) &&
            sd.ImplementationType == typeof(TeamsApprovalChannel));
    }

    [Fact]
    public void UseTeams_RegistersNamedHttpClient()
    {
        var services = BuildServices();
        // AddHttpClient registers IHttpClientFactory.
        services.Should().Contain(sd => sd.ServiceType == typeof(System.Net.Http.IHttpClientFactory));
    }

    // ------------------------------------------------------------------
    // Resolution
    // ------------------------------------------------------------------

    [Fact]
    public void UseTeams_CanResolveTeamsCallbackTokenService()
    {
        var provider = BuildServices().BuildServiceProvider();
        // TeamsCallbackTokenService does not participate in the circular chain.
        var svc = provider.GetService<TeamsCallbackTokenService>();
        svc.Should().NotBeNull();
    }

    [Fact]
    public void UseTeams_OptionsAreConfigured()
    {
        var provider = BuildServices(opts =>
        {
            opts.CallbackPath = "/my-custom-path";
        }).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<TeamsApprovalOptions>>();
        options.Value.CallbackPath.Should().Be("/my-custom-path");
        options.Value.WebhookUrl.Should().Be("https://outlook.office.com/webhook/test");
    }

    // ------------------------------------------------------------------
    // Null argument guards
    // ------------------------------------------------------------------

    [Fact]
    public void UseTeams_NullBuilder_Throws()
    {
        IApprovalsBuilder? builder = null;
        var act = () => builder!.UseTeams(_ => { });
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void UseTeams_NullConfigure_Throws()
    {
        var services = new ServiceCollection();
        var builder = services.AddApprovals();
        var act = () => builder.UseTeams(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }
}
