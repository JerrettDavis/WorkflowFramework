namespace WorkflowFramework;

/// <summary>
/// Options for configuring workflow engine behavior.
/// </summary>
public sealed class WorkflowOptions
{
    /// <summary>Gets or sets the maximum parallelism for parallel steps.</summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>Gets or sets the default step timeout.</summary>
    public TimeSpan? DefaultTimeout { get; set; }

    /// <summary>Gets or sets whether to enable compensation by default.</summary>
    public bool EnableCompensation { get; set; }

    /// <summary>Gets or sets the maximum retry attempts for retry steps.</summary>
    public int DefaultMaxRetryAttempts { get; set; } = 3;
}
