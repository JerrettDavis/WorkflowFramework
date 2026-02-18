namespace WorkflowFramework.Extensions.HumanTasks;

/// <summary>
/// Represents a human task requiring user action.
/// </summary>
public sealed class HumanTask
{
    /// <summary>Gets or sets the unique task ID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the workflow ID this task belongs to.</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>Gets or sets the task title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the task description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the assignee.</summary>
    public string Assignee { get; set; } = string.Empty;

    /// <summary>Gets or sets the task status.</summary>
    public HumanTaskStatus Status { get; set; } = HumanTaskStatus.Pending;

    /// <summary>Gets or sets when the task was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the due date.</summary>
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>Gets or sets the completion outcome.</summary>
    public string? Outcome { get; set; }

    /// <summary>Gets or sets additional data.</summary>
    public IDictionary<string, object?> Data { get; set; } = new Dictionary<string, object?>();

    /// <summary>Gets or sets the escalation rules.</summary>
    public EscalationRule? Escalation { get; set; }

    /// <summary>Gets or sets who this task was delegated to (if any).</summary>
    public string? DelegatedTo { get; set; }
}

/// <summary>
/// Represents the status of a human task.
/// </summary>
public enum HumanTaskStatus
{
    /// <summary>Task is pending.</summary>
    Pending,
    /// <summary>Task is in progress.</summary>
    InProgress,
    /// <summary>Task was approved.</summary>
    Approved,
    /// <summary>Task was rejected.</summary>
    Rejected,
    /// <summary>Task was completed.</summary>
    Completed,
    /// <summary>Task was escalated.</summary>
    Escalated,
    /// <summary>Task was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Defines escalation rules for a human task.
/// </summary>
public sealed class EscalationRule
{
    /// <summary>Gets or sets the escalation timeout.</summary>
    public TimeSpan Timeout { get; set; }

    /// <summary>Gets or sets who to escalate to.</summary>
    public string EscalateTo { get; set; } = string.Empty;

    /// <summary>Gets or sets the escalation callback.</summary>
    public Func<HumanTask, Task>? OnEscalation { get; set; }
}
