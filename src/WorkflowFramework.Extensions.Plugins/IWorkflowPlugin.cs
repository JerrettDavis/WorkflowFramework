namespace WorkflowFramework.Extensions.Plugins;

/// <summary>
/// Represents a workflow plugin that can extend framework capabilities.
/// </summary>
public interface IWorkflowPlugin : IAsyncDisposable
{
    /// <summary>Gets the unique name of this plugin.</summary>
    string Name { get; }

    /// <summary>Gets the version of this plugin.</summary>
    string Version { get; }

    /// <summary>Gets the names of plugins this plugin depends on.</summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>Configures the plugin using the provided context.</summary>
    /// <param name="context">The plugin context for registering services, steps, and middleware.</param>
    void Configure(IWorkflowPluginContext context);

    /// <summary>Initializes the plugin after configuration.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts the plugin.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops the plugin.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
