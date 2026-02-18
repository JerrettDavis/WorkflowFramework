using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace WorkflowFramework.Extensions.Plugins;

/// <summary>
/// Manages plugin registration, discovery, lifecycle, and dependency resolution.
/// </summary>
public sealed class PluginManager : IAsyncDisposable
{
    private readonly List<IWorkflowPlugin> _plugins = new();
    private readonly Dictionary<string, IWorkflowPlugin> _pluginsByName = new();
    private bool _initialized;
    private bool _started;

    /// <summary>Gets all registered plugins.</summary>
    public IReadOnlyList<IWorkflowPlugin> Plugins => _plugins;

    /// <summary>Registers a plugin instance.</summary>
    /// <param name="plugin">The plugin to register.</param>
    /// <returns>This manager for chaining.</returns>
    public PluginManager Register(IWorkflowPlugin plugin)
    {
        if (plugin == null) throw new ArgumentNullException(nameof(plugin));
        if (_pluginsByName.ContainsKey(plugin.Name))
            throw new InvalidOperationException($"Plugin '{plugin.Name}' is already registered.");
        _plugins.Add(plugin);
        _pluginsByName[plugin.Name] = plugin;
        return this;
    }

    /// <summary>Discovers and registers plugins from an assembly.</summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>This manager for chaining.</returns>
    public PluginManager DiscoverFrom(Assembly assembly)
    {
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IWorkflowPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
        foreach (var type in pluginTypes)
        {
            if (Activator.CreateInstance(type) is IWorkflowPlugin plugin)
                Register(plugin);
        }
        return this;
    }

    /// <summary>Configures all plugins in dependency order.</summary>
    /// <param name="services">The service collection.</param>
    public void ConfigureAll(IServiceCollection services)
    {
        var context = new WorkflowPluginContext(services);
        foreach (var plugin in ResolveDependencyOrder())
        {
            plugin.Configure(context);
        }
    }

    /// <summary>Initializes all plugins.</summary>
    public async Task InitializeAllAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        foreach (var plugin in ResolveDependencyOrder())
        {
            await plugin.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        _initialized = true;
    }

    /// <summary>Starts all plugins.</summary>
    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        if (_started) return;
        if (!_initialized) await InitializeAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (var plugin in ResolveDependencyOrder())
        {
            await plugin.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        _started = true;
    }

    /// <summary>Stops all plugins in reverse order.</summary>
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_started) return;
        var reversed = ResolveDependencyOrder().ToList();
        reversed.Reverse();
        foreach (var plugin in reversed)
        {
            await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        _started = false;
    }

    /// <summary>Gets a plugin by name.</summary>
    public IWorkflowPlugin? GetPlugin(string name) =>
        _pluginsByName.TryGetValue(name, out var p) ? p : null;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_started) await StopAllAsync().ConfigureAwait(false);
        foreach (var plugin in _plugins)
        {
            await plugin.DisposeAsync().ConfigureAwait(false);
        }
        _plugins.Clear();
        _pluginsByName.Clear();
    }

    private IEnumerable<IWorkflowPlugin> ResolveDependencyOrder()
    {
        var resolved = new List<IWorkflowPlugin>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var plugin in _plugins)
            Visit(plugin, resolved, visited, visiting);

        return resolved;
    }

    private void Visit(IWorkflowPlugin plugin, List<IWorkflowPlugin> resolved, HashSet<string> visited, HashSet<string> visiting)
    {
        if (visited.Contains(plugin.Name)) return;
        if (visiting.Contains(plugin.Name))
            throw new InvalidOperationException($"Circular dependency detected involving plugin '{plugin.Name}'.");

        visiting.Add(plugin.Name);
        foreach (var dep in plugin.Dependencies)
        {
            if (!_pluginsByName.TryGetValue(dep, out var depPlugin))
                throw new InvalidOperationException($"Plugin '{plugin.Name}' depends on '{dep}' which is not registered.");
            Visit(depPlugin, resolved, visited, visiting);
        }
        visiting.Remove(plugin.Name);
        visited.Add(plugin.Name);
        resolved.Add(plugin);
    }
}
