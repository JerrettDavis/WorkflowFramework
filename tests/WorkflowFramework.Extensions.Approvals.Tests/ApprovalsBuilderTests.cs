using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

public sealed class ApprovalsBuilderTests
{
    // ------------------------------------------------------------------
    // AddApprovals registers expected default services
    // ------------------------------------------------------------------

    [Fact]
    public void AddApprovals_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddApprovals();

        // Verify key registrations exist.
        services.Should().Contain(sd => sd.ServiceType == typeof(IApprovalStore));
        services.Should().Contain(sd => sd.ServiceType == typeof(PersistentApprovalService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IApprovalRouter));
        services.Should().Contain(sd => sd.ServiceType == typeof(IHostedService));
    }

    // ------------------------------------------------------------------
    // WithQuorum / WithTimeout configure options
    // ------------------------------------------------------------------

    [Fact]
    public void WithQuorum_And_WithTimeout_ConfigureOptions()
    {
        var services = new ServiceCollection();
        services.AddApprovals()
            .WithQuorum(3)
            .WithTimeout(TimeSpan.FromHours(2), OnTimeoutAction.AutoApprove);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApprovalsOptions>>();

        options.Value.RequiredApprovers.Should().Be(3);
        options.Value.Timeout.Should().Be(TimeSpan.FromHours(2));
        options.Value.OnTimeoutAction.Should().Be(OnTimeoutAction.AutoApprove);
    }

    // ------------------------------------------------------------------
    // WithPersistence<T> replaces IApprovalStore
    // ------------------------------------------------------------------

    [Fact]
    public void WithPersistence_ReplacesDefaultStore()
    {
        var services = new ServiceCollection();
        services.AddApprovals().WithPersistence<InMemoryApprovalStore>();

        var descriptors = services.Where(sd => sd.ServiceType == typeof(IApprovalStore)).ToList();
        descriptors.Should().HaveCount(1);
        descriptors[0].ImplementationType.Should().Be(typeof(InMemoryApprovalStore));
    }

    // ------------------------------------------------------------------
    // UseChannel<T> registers
    // ------------------------------------------------------------------

    [Fact]
    public void UseChannel_ByType_RegistersChannel()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseChannel<FakeChannel>();

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IApprovalChannel) &&
            sd.ImplementationType == typeof(FakeChannel));
    }

    // ------------------------------------------------------------------
    // UseChannel(instance) registers
    // ------------------------------------------------------------------

    [Fact]
    public void UseChannel_ByInstance_RegistersChannel()
    {
        var channel = Substitute.For<IApprovalChannel>();
        channel.Name.Returns("fake");

        var services = new ServiceCollection();
        services.AddApprovals().UseChannel(channel);

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IApprovalChannel) &&
            sd.ImplementationInstance == channel);
    }

    // ------------------------------------------------------------------
    // WithEscalation configures escalation chain without throwing
    // ------------------------------------------------------------------

    [Fact]
    public void WithEscalation_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var act = () => services.AddApprovals()
            .WithEscalation(e => e
                .From<FakeChannel>()
                .After(TimeSpan.FromMinutes(30))
                .To<FakeChannel>());

        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------
    // Full chain via BuildServiceProvider gives working IApprovalStore
    // ------------------------------------------------------------------

    [Fact]
    public void BuildServiceProvider_DefaultChain_CanResolveIApprovalStore()
    {
        var services = new ServiceCollection();
        services.AddApprovals();

        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IApprovalStore>();

        store.Should().NotBeNull();
        store.Should().BeOfType<InMemoryApprovalStore>();
    }

    [Fact]
    public void BuildServiceProvider_DefaultChain_CanResolvePersistentApprovalService()
    {
        var services = new ServiceCollection();
        services.AddApprovals();

        // Register at least one channel so NamedChannelRouter doesn't fail at construction.
        var channel = Substitute.For<IApprovalChannel>();
        channel.Name.Returns("test");
        services.AddSingleton(channel);

        var provider = services.BuildServiceProvider();
        var svc = provider.GetService<PersistentApprovalService>();

        svc.Should().NotBeNull();
    }

    // ------------------------------------------------------------------
    // Helper fake channel for type-based registration tests
    // ------------------------------------------------------------------

    private sealed class FakeChannel : IApprovalChannel
    {
        public string Name => "fake";

        public Task<ApprovalResponse> RequestApprovalAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
