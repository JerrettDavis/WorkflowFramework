using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// Request body for creating/updating a workflow.
/// </summary>
public sealed class CreateWorkflowRequest
{
    public string? Description { get; set; }
    public WorkflowDefinitionDto Definition { get; set; } = new();
}
