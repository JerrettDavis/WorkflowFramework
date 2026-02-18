using System.Diagnostics;

namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Provides the <see cref="ActivitySource"/> for WorkflowFramework tracing.
/// </summary>
public static class WorkflowActivitySource
{
    /// <summary>
    /// The name of the activity source.
    /// </summary>
    public const string Name = "WorkflowFramework";

    /// <summary>
    /// Gets the shared <see cref="ActivitySource"/> instance.
    /// </summary>
    public static ActivitySource Instance { get; } = new(Name);
}
