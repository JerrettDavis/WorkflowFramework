namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Information about a saved checkpoint.
/// </summary>
public sealed class CheckpointInfo
{
    /// <summary>Gets or sets the checkpoint id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the workflow id.</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>Gets or sets when the checkpoint was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the step name at time of checkpoint.</summary>
    public string? StepName { get; set; }

    /// <summary>Gets or sets the message count at time of checkpoint.</summary>
    public int MessageCount { get; set; }

    /// <summary>Gets or sets the estimated token count at time of checkpoint.</summary>
    public int EstimatedTokens { get; set; }
}
