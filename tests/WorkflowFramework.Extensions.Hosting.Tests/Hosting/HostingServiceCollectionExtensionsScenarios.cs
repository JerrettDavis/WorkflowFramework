using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Hosting;
using WorkflowFramework.Registry;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Hosting.Tests.Hosting;

[Feature("HostingServiceCollectionExtensions — DI registration helpers")]
public class HostingServiceCollectionExtensionsScenarios : TinyBddXunitBase
{
    public HostingServiceCollectionExtensionsScenarios(ITestOutputHelper output) : base(output) { }

    // ── helpers ────────────────────────────────────────────────────────────

    private static IServiceCollection BuildServices(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        return services;
    }

    // ── AddWorkflowFramework ───────────────────────────────────────────────

    [Scenario("AddWorkflowFramework registers IWorkflowRegistry"), Fact]
    public async Task AddWorkflowFramework_RegistersRegistry()
    {
        var services = BuildServices(s => s.AddWorkflowFramework());
        var provider = services.BuildServiceProvider();

        await Given("a service collection with AddWorkflowFramework called", () => provider)
            .Then("IWorkflowRegistry is resolvable", p =>
            {
                var registry = p.GetService<IWorkflowRegistry>();
                registry.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowFramework registers IWorkflowRunner"), Fact]
    public async Task AddWorkflowFramework_RegistersRunner()
    {
        var services = BuildServices(s => s.AddWorkflowFramework());
        var provider = services.BuildServiceProvider();

        await Given("a service collection with AddWorkflowFramework called", () => provider)
            .Then("IWorkflowRunner is resolvable", p =>
            {
                var runner = p.GetService<IWorkflowRunner>();
                runner.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowFramework without configure uses default options"), Fact]
    public async Task AddWorkflowFramework_DefaultOptions()
    {
        var services = BuildServices(s => s.AddWorkflowFramework());
        var provider = services.BuildServiceProvider();

        await Given("no configure action", () => provider)
            .Then("WorkflowHostingOptions is registered with defaults", p =>
            {
                var opts = p.GetService<WorkflowHostingOptions>();
                opts.Should().NotBeNull();
                opts!.MaxParallelism.Should().Be(Environment.ProcessorCount);
                opts.DefaultTimeout.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowFramework with configure applies custom options"), Fact]
    public async Task AddWorkflowFramework_CustomOptions()
    {
        var services = BuildServices(s => s.AddWorkflowFramework(o =>
        {
            o.MaxParallelism = 4;
            o.DefaultTimeout = TimeSpan.FromSeconds(30);
        }));
        var provider = services.BuildServiceProvider();

        await Given("a configure action that sets MaxParallelism=4 and timeout", () => provider)
            .Then("options reflect the custom values", p =>
            {
                var opts = p.GetService<WorkflowHostingOptions>();
                opts!.MaxParallelism.Should().Be(4);
                opts.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(30));
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowFramework also registers IWorkflowBuilder"), Fact]
    public async Task AddWorkflowFramework_RegistersBuilder()
    {
        var services = BuildServices(s => s.AddWorkflowFramework());
        var provider = services.BuildServiceProvider();

        await Given("AddWorkflowFramework called on service collection", () => provider)
            .Then("IWorkflowBuilder is resolvable from DI", p =>
            {
                var builder = p.GetService<IWorkflowBuilder>();
                builder.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowHostedServices registers IWorkflowScheduler"), Fact]
    public async Task AddWorkflowHostedServices_RegistersScheduler()
    {
        var services = BuildServices(s =>
        {
            s.AddWorkflowFramework();
            s.AddWorkflowHostedServices();
        });
        var provider = services.BuildServiceProvider();

        await Given("AddWorkflowHostedServices called after AddWorkflowFramework", () => provider)
            .Then("IWorkflowScheduler is resolvable", p =>
            {
                var scheduler = p.GetService<WorkflowFramework.Extensions.Scheduling.IWorkflowScheduler>();
                scheduler.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AddWorkflowFramework is idempotent when called twice"), Fact]
    public async Task AddWorkflowFramework_CalledTwice_DoesNotThrow()
    {
        Exception? caught = null;
        IServiceProvider? provider = null;
        try
        {
            var services = BuildServices(s =>
            {
                s.AddWorkflowFramework();
                s.AddWorkflowFramework();
            });
            provider = services.BuildServiceProvider();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        await Given("AddWorkflowFramework called twice", () => (caught, provider))
            .Then("no exception is thrown and provider builds successfully", t =>
            {
                t.caught.Should().BeNull();
                t.provider.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
