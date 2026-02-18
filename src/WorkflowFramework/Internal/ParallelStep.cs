namespace WorkflowFramework.Internal;

/// <summary>
/// A step that executes multiple steps in parallel.
/// </summary>
internal sealed class ParallelStep : IStep
{
    private readonly IReadOnlyList<IStep> _steps;

    public ParallelStep(IReadOnlyList<IStep> steps)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    public string Name => $"Parallel({string.Join(", ", _steps.Select(s => s.Name))})";

    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var tasks = _steps.Select(step => step.ExecuteAsync(context));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
