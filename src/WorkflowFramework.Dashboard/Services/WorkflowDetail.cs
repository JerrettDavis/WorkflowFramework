namespace WorkflowFramework.Dashboard.Services;

/// <summary>
/// Detailed information about a workflow including its steps.
/// </summary>
public sealed class WorkflowDetail
{
    /// <summary>Gets or sets the workflow name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the step names in order.</summary>
    public List<string> Steps { get; set; } = new();
}
