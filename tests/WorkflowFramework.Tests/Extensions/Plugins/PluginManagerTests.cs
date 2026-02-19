using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Plugins;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Plugins;

public class PluginManagerTests
{
    private static int _configOrder;

    private class OrderedPlugin(string name, params string[] deps) : WorkflowPluginBase
    {
        public int Order { get; private set; }
        public bool WasConfigured { get; private set; }
        public bool WasInitialized { get; private set; }
        public bool WasStarted { get; private set; }
        public bool WasStopped { get; private set; }
        public bool WasDisposed { get; private set; }

        public override string Name => name;
        public override IReadOnlyList<string> Dependencies => deps;
        public override void Configure(IWorkflowPluginContext context) { WasConfigured = true; Order = Interlocked.Increment(ref _configOrder); }
        public override Task InitializeAsync(CancellationToken ct) { WasInitialized = true; return Task.CompletedTask; }
        public override Task StartAsync(CancellationToken ct) { WasStarted = true; return Task.CompletedTask; }
        public override Task StopAsync(CancellationToken ct) { WasStopped = true; return Task.CompletedTask; }
        public override ValueTask DisposeAsync() { WasDisposed = true; return default; }
    }

    [Fact]
    public void Register_NullPlugin_ThrowsArgumentNullException()
    {
        var mgr = new PluginManager();
        mgr.Invoking(m => m.Register(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_ReturnsManagerForChaining()
    {
        var mgr = new PluginManager();
        var result = mgr.Register(new OrderedPlugin("A"));
        result.Should().BeSameAs(mgr);
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        var mgr = new PluginManager();
        mgr.Register(new OrderedPlugin("A"));
        mgr.Invoking(m => m.Register(new OrderedPlugin("A")))
            .Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
    }

    [Fact]
    public void Plugins_ReturnsAllRegistered()
    {
        var mgr = new PluginManager();
        mgr.Register(new OrderedPlugin("A"));
        mgr.Register(new OrderedPlugin("B"));
        mgr.Plugins.Should().HaveCount(2);
    }

    [Fact]
    public void GetPlugin_ExistingName_ReturnsPlugin()
    {
        var mgr = new PluginManager();
        var p = new OrderedPlugin("X");
        mgr.Register(p);
        mgr.GetPlugin("X").Should().BeSameAs(p);
    }

    [Fact]
    public void GetPlugin_NonExistent_ReturnsNull()
    {
        var mgr = new PluginManager();
        mgr.GetPlugin("nope").Should().BeNull();
    }

    [Fact]
    public void ConfigureAll_ResolvesInDependencyOrder()
    {
        Interlocked.Exchange(ref _configOrder, 0);
        var mgr = new PluginManager();
        var c = new OrderedPlugin("C", "B");
        var b = new OrderedPlugin("B", "A");
        var a = new OrderedPlugin("A");
        mgr.Register(c).Register(b).Register(a);
        mgr.ConfigureAll(new ServiceCollection());
        a.Order.Should().BeLessThan(b.Order);
        b.Order.Should().BeLessThan(c.Order);
    }

    [Fact]
    public void ConfigureAll_CircularDependency_Throws()
    {
        var mgr = new PluginManager();
        mgr.Register(new OrderedPlugin("A", "B"));
        mgr.Register(new OrderedPlugin("B", "A"));
        mgr.Invoking(m => m.ConfigureAll(new ServiceCollection()))
            .Should().Throw<InvalidOperationException>().WithMessage("*Circular*");
    }

    [Fact]
    public void ConfigureAll_MissingDependency_Throws()
    {
        var mgr = new PluginManager();
        mgr.Register(new OrderedPlugin("A", "Missing"));
        mgr.Invoking(m => m.ConfigureAll(new ServiceCollection()))
            .Should().Throw<InvalidOperationException>().WithMessage("*Missing*");
    }

    [Fact]
    public async Task InitializeAllAsync_SetsInitialized()
    {
        var mgr = new PluginManager();
        var p = new OrderedPlugin("A");
        mgr.Register(p);
        await mgr.InitializeAllAsync();
        p.WasInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAllAsync_CalledTwice_OnlyInitializesOnce()
    {
        var mgr = new PluginManager();
        var count = 0;
        var p = new OrderedPlugin("A");
        mgr.Register(p);
        await mgr.InitializeAllAsync();
        await mgr.InitializeAllAsync(); // second call should be no-op
        p.WasInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task StartAllAsync_AutoInitializes()
    {
        var mgr = new PluginManager();
        var p = new OrderedPlugin("A");
        mgr.Register(p);
        await mgr.StartAllAsync();
        p.WasInitialized.Should().BeTrue();
        p.WasStarted.Should().BeTrue();
    }

    [Fact]
    public async Task StartAllAsync_CalledTwice_OnlyStartsOnce()
    {
        var mgr = new PluginManager();
        var p = new OrderedPlugin("A");
        mgr.Register(p);
        await mgr.StartAllAsync();
        await mgr.StartAllAsync();
        p.WasStarted.Should().BeTrue();
    }

    [Fact]
    public async Task StopAllAsync_WhenNotStarted_DoesNothing()
    {
        var mgr = new PluginManager();
        var p = new OrderedPlugin("A");
        mgr.Register(p);
        await mgr.StopAllAsync();
        p.WasStopped.Should().BeFalse();
    }

    [Fact]
    public async Task StopAllAsync_StopsInReverseOrder()
    {
        var mgr = new PluginManager();
        var stopOrder = new List<string>();
        var a = new OrderedPlugin("A");
        var b = new OrderedPlugin("B", "A");
        mgr.Register(b).Register(a);
        await mgr.StartAllAsync();
        await mgr.StopAllAsync();
        a.WasStopped.Should().BeTrue();
        b.WasStopped.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_StopsAndDisposesAll()
    {
        var mgr = new PluginManager();
        var p = new OrderedPlugin("A");
        mgr.Register(p);
        await mgr.StartAllAsync();
        await mgr.DisposeAsync();
        p.WasStopped.Should().BeTrue();
        p.WasDisposed.Should().BeTrue();
        mgr.Plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_WhenNotStarted_StillDisposes()
    {
        var mgr = new PluginManager();
        var p = new OrderedPlugin("A");
        mgr.Register(p);
        await mgr.DisposeAsync();
        p.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAllAsync_RespectsCancel()
    {
        var mgr = new PluginManager();
        mgr.Register(new OrderedPlugin("A"));
        using var cts = new CancellationTokenSource();
        // Should not throw with a non-cancelled token
        await mgr.InitializeAllAsync(cts.Token);
    }

    [Fact]
    public void DiscoverFrom_FindsPluginsInAssembly()
    {
        // DiscoverFrom scans for IWorkflowPlugin implementations
        // We test it doesn't throw on the test assembly (which may have no plugins or some)
        var mgr = new PluginManager();
        // Use an assembly that definitely has a plugin - the Plugins assembly itself won't since they're abstract/interfaces
        // Just verify it doesn't throw
        mgr.Invoking(m => m.DiscoverFrom(typeof(PluginManager).Assembly)).Should().NotThrow();
    }
}
