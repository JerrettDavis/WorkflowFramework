using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Agents;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Agents.Tests.Agents;

[Feature("ServiceCollectionExtensions (Agents) — agent tooling DI registration")]
public class AgentServiceCollectionExtensionsScenarios : TinyBddXunitBase
{
    public AgentServiceCollectionExtensionsScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("AddAgentTooling registers ToolRegistry as singleton"), Fact]
    public async Task AddAgentTooling_RegistersToolRegistry()
    {
        var services = new ServiceCollection().AddAgentTooling();
        var provider = services.BuildServiceProvider();

        await Given("AddAgentTooling called", () => provider)
            .Then("ToolRegistry is resolvable", p =>
            {
                p.GetService<ToolRegistry>().Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToolRegistry singleton returns same instance"), Fact]
    public async Task AddAgentTooling_ToolRegistryIsSingleton()
    {
        var services = new ServiceCollection().AddAgentTooling();
        var provider = services.BuildServiceProvider();

        await Given("ToolRegistry registered as singleton", () => provider)
            .Then("two resolutions return same instance", p =>
            {
                var r1 = p.GetRequiredService<ToolRegistry>();
                var r2 = p.GetRequiredService<ToolRegistry>();
                r1.Should().BeSameAs(r2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddAgentTooling with configure applies options"), Fact]
    public async Task AddAgentTooling_ConfigureAction_AppliesOptions()
    {
        var services = new ServiceCollection().AddAgentTooling(opts => opts.MaxToolConcurrency = 8);
        var provider = services.BuildServiceProvider();

        await Given("configure action sets MaxToolConcurrency=8", () => provider)
            .Then("AgentToolingOptions.MaxToolConcurrency is 8", p =>
            {
                var opts = p.GetService<AgentToolingOptions>();
                opts!.MaxToolConcurrency.Should().Be(8);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IToolProvider registrations are auto-wired into ToolRegistry"), Fact]
    public async Task AddAgentTooling_AutoWiresToolProviders()
    {
        var mockProvider = Substitute.For<IToolProvider>();
        mockProvider.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ToolDefinition>>(new List<ToolDefinition>()));

        var services = new ServiceCollection();
        services.AddSingleton<IToolProvider>(mockProvider);
        services.AddAgentTooling();
        var provider = services.BuildServiceProvider();

        await Given("one IToolProvider registered before AddAgentTooling", () => provider)
            .Then("ToolRegistry.Providers contains the registered provider", p =>
            {
                var registry = p.GetRequiredService<ToolRegistry>();
                registry.Providers.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Calling AddAgentTooling twice is idempotent"), Fact]
    public async Task AddAgentTooling_CalledTwice_IsIdempotent()
    {
        Exception? caught = null;
        IServiceProvider? provider = null;
        try
        {
            var services = new ServiceCollection();
            services.AddAgentTooling();
            services.AddAgentTooling();
            provider = services.BuildServiceProvider();
        }
        catch (Exception ex) { caught = ex; }

        await Given("AddAgentTooling called twice", () => (caught, provider))
            .Then("no exception; ToolRegistry resolves once (TryAddSingleton semantics)", t =>
            {
                t.caught.Should().BeNull();
                t.provider!.GetRequiredService<ToolRegistry>().Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
