namespace WorkflowFramework.Samples.TaskStream.Models;

/// <summary>
/// The final result of the TaskStream pipeline.
/// </summary>
public sealed class TaskStreamResult
{
    /// <summary>Gets or sets all processed todo items.</summary>
    public List<TodoItem> ProcessedItems { get; set; } = [];

    /// <summary>Gets or sets results from automated execution.</summary>
    public List<AutomatedResult> AutomatedResults { get; set; } = [];

    /// <summary>Gets or sets human tasks enriched with suggestions.</summary>
    public List<TodoItem> EnrichedItems { get; set; } = [];

    /// <summary>Gets or sets the rendered markdown report.</summary>
    public string MarkdownReport { get; set; } = string.Empty;

    /// <summary>Gets or sets pipeline statistics.</summary>
    public PipelineStats Stats { get; set; } = new();
}

/// <summary>
/// Result of an automated task execution.
/// </summary>
public sealed class AutomatedResult
{
    /// <summary>Gets or sets the task that was automated.</summary>
    public required TodoItem Task { get; set; }

    /// <summary>Gets or sets the execution result.</summary>
    public string Result { get; set; } = string.Empty;
}

/// <summary>
/// Statistics about the pipeline execution.
/// </summary>
public sealed class PipelineStats
{
    /// <summary>Gets or sets total source messages processed.</summary>
    public int TotalMessages { get; set; }

    /// <summary>Gets or sets total tasks extracted.</summary>
    public int TotalTasks { get; set; }

    /// <summary>Gets or sets number of automated tasks.</summary>
    public int AutomatedCount { get; set; }

    /// <summary>Gets or sets number of human-required tasks.</summary>
    public int HumanCount { get; set; }

    /// <summary>Gets or sets number of duplicates removed.</summary>
    public int DuplicatesRemoved { get; set; }

    /// <summary>Gets or sets pipeline duration.</summary>
    public TimeSpan Duration { get; set; }
}
