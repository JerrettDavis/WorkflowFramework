namespace WorkflowFramework.Attributes;

/// <summary>
/// Specifies a custom name for a workflow step.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StepNameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="StepNameAttribute"/>.
    /// </summary>
    /// <param name="name">The step name.</param>
    public StepNameAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the step name.
    /// </summary>
    public string Name { get; }
}

/// <summary>
/// Specifies a description for a workflow step.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StepDescriptionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="StepDescriptionAttribute"/>.
    /// </summary>
    /// <param name="description">The step description.</param>
    public StepDescriptionAttribute(string description)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    /// <summary>
    /// Gets the step description.
    /// </summary>
    public string Description { get; }
}

/// <summary>
/// Specifies a timeout for a workflow step.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StepTimeoutAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="StepTimeoutAttribute"/>.
    /// </summary>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    public StepTimeoutAttribute(double timeoutSeconds)
    {
        TimeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// Gets the timeout in seconds.
    /// </summary>
    public double TimeoutSeconds { get; }
}

/// <summary>
/// Specifies retry behavior for a workflow step.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StepRetryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="StepRetryAttribute"/>.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of retry attempts.</param>
    /// <param name="backoffMs">The backoff delay in milliseconds between retries.</param>
    public StepRetryAttribute(int maxAttempts, int backoffMs = 0)
    {
        MaxAttempts = maxAttempts;
        BackoffMs = backoffMs;
    }

    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the backoff delay in milliseconds.
    /// </summary>
    public int BackoffMs { get; }
}

/// <summary>
/// Specifies the execution order for a step when using auto-discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StepOrderAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="StepOrderAttribute"/>.
    /// </summary>
    /// <param name="order">The execution order.</param>
    public StepOrderAttribute(int order)
    {
        Order = order;
    }

    /// <summary>
    /// Gets the execution order.
    /// </summary>
    public int Order { get; }
}

/// <summary>
/// Marks a class as a named workflow for registry auto-discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WorkflowAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowAttribute"/>.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    public WorkflowAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the workflow name.
    /// </summary>
    public string Name { get; }
}
