using WorkflowFramework.Attributes;

namespace WorkflowFramework;

/// <summary>
/// Abstract base class for workflow steps with common patterns.
/// </summary>
public abstract class StepBase : IStep
{
    /// <inheritdoc />
    public virtual string Name =>
        GetType().GetCustomAttributes(typeof(StepNameAttribute), false) is StepNameAttribute[] { Length: > 0 } attrs
            ? attrs[0].Name
            : GetType().Name;

    /// <inheritdoc />
    public abstract Task ExecuteAsync(IWorkflowContext context);
}

/// <summary>
/// Abstract base class for typed workflow steps with common patterns.
/// </summary>
/// <typeparam name="TData">The workflow data type.</typeparam>
public abstract class StepBase<TData> : IStep<TData> where TData : class
{
    /// <inheritdoc />
    public virtual string Name =>
        GetType().GetCustomAttributes(typeof(StepNameAttribute), false) is StepNameAttribute[] { Length: > 0 } attrs
            ? attrs[0].Name
            : GetType().Name;

    /// <inheritdoc />
    public abstract Task ExecuteAsync(IWorkflowContext<TData> context);
}
