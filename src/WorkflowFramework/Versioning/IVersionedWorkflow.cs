namespace WorkflowFramework.Versioning;

/// <summary>
/// Represents a workflow with version information.
/// </summary>
public interface IVersionedWorkflow : IWorkflow
{
    /// <summary>
    /// Gets the version of this workflow.
    /// </summary>
    int Version { get; }
}

/// <summary>
/// Represents a typed workflow with version information.
/// </summary>
/// <typeparam name="TData">The workflow data type.</typeparam>
public interface IVersionedWorkflow<TData> : IWorkflow<TData> where TData : class
{
    /// <summary>
    /// Gets the version of this workflow.
    /// </summary>
    int Version { get; }
}

/// <summary>
/// Registry that supports versioned workflow resolution.
/// </summary>
public interface IVersionedWorkflowRegistry
{
    /// <summary>
    /// Registers a versioned workflow factory.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <param name="version">The version number.</param>
    /// <param name="factory">The factory function.</param>
    void Register(string name, int version, Func<IWorkflow> factory);

    /// <summary>
    /// Resolves a workflow by name and version. If version is null, returns the latest.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <param name="version">The specific version, or null for latest.</param>
    /// <returns>The workflow instance.</returns>
    IWorkflow Resolve(string name, int? version = null);

    /// <summary>
    /// Gets all registered versions for a workflow name.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <returns>The available versions.</returns>
    IReadOnlyCollection<int> GetVersions(string name);
}

/// <summary>
/// Default implementation of <see cref="IVersionedWorkflowRegistry"/>.
/// </summary>
public sealed class VersionedWorkflowRegistry : IVersionedWorkflowRegistry
{
    private readonly Dictionary<string, SortedDictionary<int, Func<IWorkflow>>> _workflows = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(string name, int version, Func<IWorkflow> factory)
    {
        if (!_workflows.TryGetValue(name, out var versions))
        {
            versions = new SortedDictionary<int, Func<IWorkflow>>();
            _workflows[name] = versions;
        }
        versions[version] = factory;
    }

    /// <inheritdoc />
    public IWorkflow Resolve(string name, int? version = null)
    {
        if (!_workflows.TryGetValue(name, out var versions) || versions.Count == 0)
            throw new KeyNotFoundException($"No workflow registered with name '{name}'.");

        if (version.HasValue)
        {
            if (!versions.TryGetValue(version.Value, out var factory))
                throw new KeyNotFoundException($"Workflow '{name}' version {version.Value} not found.");
            return factory();
        }

        return versions.Last().Value();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetVersions(string name)
    {
        if (!_workflows.TryGetValue(name, out var versions))
            return Array.Empty<int>();
        return versions.Keys;
    }
}
