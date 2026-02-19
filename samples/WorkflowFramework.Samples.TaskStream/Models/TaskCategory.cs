namespace WorkflowFramework.Samples.TaskStream.Models;

/// <summary>
/// Categorizes a task by how it should be handled.
/// </summary>
public enum TaskCategory
{
    /// <summary>Task can be fully automated by an agent.</summary>
    Automatable,
    /// <summary>Task requires human action.</summary>
    HumanRequired,
    /// <summary>Task needs both human and automated steps.</summary>
    Hybrid
}
