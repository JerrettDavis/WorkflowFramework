namespace WorkflowFramework.Extensions.Plugins;

/// <summary>
/// Base class for workflow plugins with sensible defaults.
/// </summary>
public abstract class WorkflowPluginBase : IWorkflowPlugin
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public virtual string Version => "1.0.0";

    /// <inheritdoc />
    public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();

    /// <inheritdoc />
    public abstract void Configure(IWorkflowPluginContext context);

    /// <inheritdoc />
    public virtual Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual ValueTask DisposeAsync() => default;
}
