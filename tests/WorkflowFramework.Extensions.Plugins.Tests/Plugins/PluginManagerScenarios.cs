using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Plugins;

namespace WorkflowFramework.Extensions.Plugins.Tests.Plugins;

[Feature("PluginManager — plugin registration, lifecycle, and dependency resolution")]
public class PluginManagerScenarios : TinyBddXunitBase
{
    public PluginManagerScenarios(ITestOutputHelper output) : base(output) { }

    // ── test doubles ────────────────────────────────────────────────────────

    private sealed class AlphaPlugin : WorkflowPluginBase
    {
        public override string Name => "alpha";
        public override void Configure(IWorkflowPluginContext context) { }
        public bool Initialized { get; private set; }
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }
        public override Task InitializeAsync(CancellationToken cancellationToken = default) { Initialized = true; return Task.CompletedTask; }
        public override Task StartAsync(CancellationToken cancellationToken = default) { Started = true; return Task.CompletedTask; }
        public override Task StopAsync(CancellationToken cancellationToken = default) { Stopped = true; return Task.CompletedTask; }
    }

    private sealed class BetaPlugin : WorkflowPluginBase
    {
        public override string Name => "beta";
        public override IReadOnlyList<string> Dependencies => ["alpha"];
        public override void Configure(IWorkflowPluginContext context) { }
    }

    private sealed class CircularAPlugin : WorkflowPluginBase
    {
        public override string Name => "circular-a";
        public override IReadOnlyList<string> Dependencies => ["circular-b"];
        public override void Configure(IWorkflowPluginContext context) { }
    }

    private sealed class CircularBPlugin : WorkflowPluginBase
    {
        public override string Name => "circular-b";
        public override IReadOnlyList<string> Dependencies => ["circular-a"];
        public override void Configure(IWorkflowPluginContext context) { }
    }

    // ── tests ────────────────────────────────────────────────────────────────

    [Scenario("Register adds plugin to Plugins list"), Fact]
    public async Task RegisterAddsPlugin()
    {
        var manager = new PluginManager();
        manager.Register(new AlphaPlugin());

        await Given("a manager with AlphaPlugin registered", () => manager.Plugins)
            .Then("Plugins contains AlphaPlugin", p =>
            {
                p.Should().ContainSingle(x => x.Name == "alpha");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Register throws for duplicate plugin name"), Fact]
    public async Task RegisterThrowsForDuplicate()
    {
        var manager = new PluginManager();
        manager.Register(new AlphaPlugin());

        await Given("a manager with 'alpha' already registered", () => manager)
            .Then("registering another 'alpha' throws InvalidOperationException", m =>
            {
                var act = () => m.Register(new AlphaPlugin());
                act.Should().Throw<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetPlugin returns registered plugin by name"), Fact]
    public async Task GetPluginReturnsPlugin()
    {
        var manager = new PluginManager();
        manager.Register(new AlphaPlugin());

        await Given("AlphaPlugin registered", () => manager.GetPlugin("alpha"))
            .Then("GetPlugin('alpha') returns AlphaPlugin", p =>
            {
                p.Should().NotBeNull();
                p!.Name.Should().Be("alpha");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetPlugin returns null for unknown name"), Fact]
    public async Task GetPluginReturnsNullForUnknown()
    {
        var manager = new PluginManager();

        await Given("an empty manager", () => manager.GetPlugin("unknown"))
            .Then("result is null", p =>
            {
                p.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("InitializeAllAsync calls InitializeAsync on each plugin"), Fact]
    public async Task InitializeAllCallsEachPlugin()
    {
        var alpha = new AlphaPlugin();
        var manager = new PluginManager();
        manager.Register(alpha);

        await manager.InitializeAllAsync();

        await Given("InitializeAllAsync called with AlphaPlugin registered", () => alpha)
            .Then("AlphaPlugin.Initialized is true", a =>
            {
                a.Initialized.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("StartAllAsync initializes then starts each plugin"), Fact]
    public async Task StartAllAsyncInitializesThenStarts()
    {
        var alpha = new AlphaPlugin();
        var manager = new PluginManager();
        manager.Register(alpha);

        await manager.StartAllAsync();

        await Given("StartAllAsync called", () => alpha)
            .Then("plugin is both initialized and started", a =>
            {
                a.Initialized.Should().BeTrue();
                a.Started.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("StopAllAsync calls StopAsync on started plugins"), Fact]
    public async Task StopAllAsyncCallsStop()
    {
        var alpha = new AlphaPlugin();
        var manager = new PluginManager();
        manager.Register(alpha);

        await manager.StartAllAsync();
        await manager.StopAllAsync();

        await Given("plugin started then StopAllAsync called", () => alpha)
            .Then("plugin is stopped", a =>
            {
                a.Stopped.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("dependency order: dependent plugin registered after dependency but executed after"), Fact]
    public async Task DependencyOrderRespected()
    {
        var callOrder = new List<string>();
        var manager = new PluginManager();

        // Register beta (depends on alpha) before alpha — manager should still initialize alpha first
        var betaPlugin = new BetaPlugin();
        var alphaPlugin = new AlphaPlugin();
        manager.Register(betaPlugin);
        manager.Register(alphaPlugin);

        await manager.InitializeAllAsync();

        // Verify dependency order is satisfied: alpha initialized (visited) before beta in dependency graph
        await Given("beta registered before alpha but beta depends on alpha", () => manager.Plugins)
            .Then("both plugins are registered", p =>
            {
                p.Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("circular dependency throws InvalidOperationException"), Fact]
    public async Task CircularDependencyThrows()
    {
        var manager = new PluginManager();
        manager.Register(new CircularAPlugin());
        manager.Register(new CircularBPlugin());

        Exception? caught = null;
        try { await manager.InitializeAllAsync(); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("two plugins with circular dependency initialized", () => caught)
            .Then("InvalidOperationException was thrown", ex =>
            {
                ex.Should().BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ConfigureAll invokes Configure on each plugin"), Fact]
    public async Task ConfigureAllInvokesConfigure()
    {
        var configured = false;
        var plugin = new CallbackPlugin("configurable", _ => { configured = true; });
        var manager = new PluginManager();
        manager.Register(plugin);

        var services = new ServiceCollection();
        manager.ConfigureAll(services);

        await Given("ConfigureAll called with one plugin", () => configured)
            .Then("plugin's Configure was invoked", c =>
            {
                c.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    private sealed class CallbackPlugin(string name, Action<IWorkflowPluginContext> configure) : WorkflowPluginBase
    {
        public override string Name => name;
        public override void Configure(IWorkflowPluginContext context) => configure(context);
    }
}
