namespace WorkflowFramework.Samples.TaskStream.Models;

/// <summary>
/// Represents a task/todo item extracted from a source message.
/// </summary>
public sealed class TodoItem
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Gets or sets the source type (email, discord, webhook, file).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the source message identifier.</summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the task title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the task description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the assigned people.</summary>
    public List<string> Assignees { get; set; } = [];

    /// <summary>Gets or sets the due date.</summary>
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>Gets or sets the recurrence pattern (e.g., "weekly", "daily").</summary>
    public string? Recurrence { get; set; }

    /// <summary>Gets or sets the task status.</summary>
    public TodoStatus Status { get; set; } = TodoStatus.Pending;

    /// <summary>Gets or sets the task category.</summary>
    public TaskCategory Category { get; set; } = TaskCategory.HumanRequired;

    /// <summary>Gets or sets the priority (1=low, 4=urgent).</summary>
    public int Priority { get; set; } = 1;

    /// <summary>Gets or sets enrichment data added during processing.</summary>
    public Dictionary<string, string> Enrichments { get; set; } = [];

    /// <summary>Gets or sets when the item was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the item was completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets a content hash for deduplication.</summary>
    public string ContentHash => $"{Title.ToLowerInvariant().Trim()}".GetHashCode().ToString("x8");
}

/// <summary>
/// Task completion status.
/// </summary>
public enum TodoStatus
{
    /// <summary>Task is pending.</summary>
    Pending,
    /// <summary>Task is in progress.</summary>
    InProgress,
    /// <summary>Task is completed.</summary>
    Completed,
    /// <summary>Task failed automation.</summary>
    Failed
}
