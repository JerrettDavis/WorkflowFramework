namespace WorkflowFramework.Testing;

/// <summary>
/// Fluent builder for constructing test scenarios.
/// </summary>
public sealed class WorkflowTestBuilder
{
    private readonly Dictionary<string, object?> _properties = new();
    private readonly List<(string Name, IStep Step)> _overrides = new();
    private CancellationToken _cancellationToken;

    /// <summary>Sets a context property.</summary>
    public WorkflowTestBuilder WithProperty(string key, object? value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>Overrides a step.</summary>
    public WorkflowTestBuilder WithStepOverride(string name, IStep step)
    {
        _overrides.Add((name, step));
        return this;
    }

    /// <summary>Sets the cancellation token.</summary>
    public WorkflowTestBuilder WithCancellation(CancellationToken token)
    {
        _cancellationToken = token;
        return this;
    }

    /// <summary>Builds a test harness and context, then executes the workflow.</summary>
    public async Task<WorkflowResult> ExecuteAsync(IWorkflow workflow)
    {
        var harness = new WorkflowTestHarness();
        foreach (var (name, step) in _overrides)
            harness.OverrideStep(name, step);

        var context = new WorkflowContext(_cancellationToken);
        foreach (var kvp in _properties)
            context.Properties[kvp.Key] = kvp.Value;

        return await harness.ExecuteAsync(workflow, context).ConfigureAwait(false);
    }
}
