namespace WorkflowFramework.Tests.Common;

/// <summary>
/// A simple step that appends its name to a list in the context properties.
/// </summary>
public class TrackingStep : IStep
{
    public TrackingStep(string name = "TrackingStep")
    {
        Name = name;
    }

    public string Name { get; }

    public Task ExecuteAsync(IWorkflowContext context)
    {
        GetLog(context).Add(Name);
        return Task.CompletedTask;
    }

    public static List<string> GetLog(IWorkflowContext context)
    {
        const string key = "StepLog";
        if (!context.Properties.TryGetValue(key, out var val) || val is not List<string> log)
        {
            log = new List<string>();
            context.Properties[key] = log;
        }
        return log;
    }
}

/// <summary>
/// A step that always throws.
/// </summary>
public class FailingStep : IStep
{
    public string Name => "FailingStep";

    public Task ExecuteAsync(IWorkflowContext context) =>
        throw new InvalidOperationException("Step failed intentionally.");
}

/// <summary>
/// A compensating step that tracks execution and compensation.
/// </summary>
public class CompensatingTrackingStep : ICompensatingStep
{
    public CompensatingTrackingStep(string name = "CompensatingTrackingStep")
    {
        Name = name;
    }

    public string Name { get; }

    public Task ExecuteAsync(IWorkflowContext context)
    {
        TrackingStep.GetLog(context).Add($"{Name}:Execute");
        return Task.CompletedTask;
    }

    public Task CompensateAsync(IWorkflowContext context)
    {
        TrackingStep.GetLog(context).Add($"{Name}:Compensate");
        return Task.CompletedTask;
    }
}

/// <summary>
/// A typed step for testing.
/// </summary>
public class TypedTrackingStep<TData> : IStep<TData> where TData : class
{
    public TypedTrackingStep(string name = "TypedTrackingStep")
    {
        Name = name;
    }

    public string Name { get; }

    public Task ExecuteAsync(IWorkflowContext<TData> context)
    {
        TrackingStep.GetLog(context).Add(Name);
        return Task.CompletedTask;
    }
}
