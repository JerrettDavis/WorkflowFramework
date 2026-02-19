namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// Summary item returned when listing workflows.
/// </summary>
public sealed class WorkflowListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public int StepCount { get; set; }
}
