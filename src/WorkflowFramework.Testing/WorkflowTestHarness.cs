namespace WorkflowFramework.Testing;

/// <summary>
/// Test harness for executing workflows with mock steps and capturing results.
/// </summary>
public sealed class WorkflowTestHarness
{
    private readonly Dictionary<string, IStep> _stepOverrides = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Overrides a step by name with a replacement.
    /// </summary>
    /// <param name="stepName">The name of the step to override.</param>
    /// <param name="replacement">The replacement step.</param>
    /// <returns>This harness for chaining.</returns>
    public WorkflowTestHarness OverrideStep(string stepName, IStep replacement)
    {
        _stepOverrides[stepName] = replacement;
        return this;
    }

    /// <summary>
    /// Overrides a step by name with a fake step action.
    /// </summary>
    /// <param name="stepName">The name of the step to override.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>This harness for chaining.</returns>
    public WorkflowTestHarness OverrideStep(string stepName, Func<IWorkflowContext, Task> action)
    {
        _stepOverrides[stepName] = new FakeStep(stepName, action);
        return this;
    }

    /// <summary>
    /// Executes a workflow using the configured overrides.
    /// </summary>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="context">The workflow context.</param>
    /// <returns>The workflow result.</returns>
    public Task<WorkflowResult> ExecuteAsync(IWorkflow workflow, IWorkflowContext context)
    {
        if (_stepOverrides.Count > 0)
        {
            // Build a new workflow with overridden steps
            var builder = Workflow.Create(workflow.Name);
            foreach (var step in workflow.Steps)
            {
                if (_stepOverrides.TryGetValue(step.Name, out var replacement))
                    builder.Step(replacement);
                else
                    builder.Step(step);
            }
            workflow = builder.Build();
        }

        return workflow.ExecuteAsync(context);
    }

    /// <summary>
    /// Executes a typed workflow using the configured overrides.
    /// If step overrides are configured, the workflow is rebuilt with the overridden steps.
    /// </summary>
    /// <typeparam name="TData">The workflow data type.</typeparam>
    /// <param name="workflow">The workflow to execute.</param>
    /// <param name="data">The initial data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The workflow result.</returns>
    public async Task<WorkflowResult<TData>> ExecuteAsync<TData>(IWorkflow<TData> workflow, TData data, CancellationToken cancellationToken = default) where TData : class
    {
        var context = new WorkflowContext<TData>(data, cancellationToken);

        if (_stepOverrides.Count > 0)
        {
            // Build an untyped workflow with overrides, then wrap it as typed
            var builder = Workflow.Create(workflow.Name);
            // Try to get steps from the workflow if it exposes them via IWorkflow
            if (workflow is IWorkflow untypedWorkflow)
            {
                foreach (var step in untypedWorkflow.Steps)
                {
                    if (_stepOverrides.TryGetValue(step.Name, out var replacement))
                        builder.Step(replacement);
                    else
                        builder.Step(step);
                }
                var rebuilt = builder.Build();
                var result = await rebuilt.ExecuteAsync(context).ConfigureAwait(false);
                return new WorkflowResult<TData>(result.Status, context);
            }
        }

        return await workflow.ExecuteAsync(context).ConfigureAwait(false);
    }
}

/// <summary>
/// A configurable fake step for testing.
/// </summary>
public sealed class FakeStep : IStep
{
    private readonly Func<IWorkflowContext, Task>? _action;

    /// <summary>
    /// Initializes a new instance of <see cref="FakeStep"/>.
    /// </summary>
    /// <param name="name">The step name.</param>
    /// <param name="action">The action to execute (optional, defaults to no-op).</param>
    public FakeStep(string name, Func<IWorkflowContext, Task>? action = null)
    {
        Name = name;
        _action = action;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Gets the number of times this step has been executed.
    /// </summary>
    public int ExecutionCount { get; private set; }

    /// <summary>
    /// Gets the contexts from each execution.
    /// </summary>
    public List<IWorkflowContext> ExecutionContexts { get; } = new();

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        ExecutionCount++;
        ExecutionContexts.Add(context);
        if (_action != null)
            await _action(context).ConfigureAwait(false);
    }
}

/// <summary>
/// A typed configurable fake step for testing.
/// </summary>
/// <typeparam name="TData">The workflow data type.</typeparam>
public sealed class FakeStep<TData> : IStep<TData> where TData : class
{
    private readonly Func<IWorkflowContext<TData>, Task>? _action;

    /// <summary>
    /// Initializes a new instance of <see cref="FakeStep{TData}"/>.
    /// </summary>
    /// <param name="name">The step name.</param>
    /// <param name="action">The action to execute (optional, defaults to no-op).</param>
    public FakeStep(string name, Func<IWorkflowContext<TData>, Task>? action = null)
    {
        Name = name;
        _action = action;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Gets the number of times this step has been executed.
    /// </summary>
    public int ExecutionCount { get; private set; }

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext<TData> context)
    {
        ExecutionCount++;
        if (_action != null)
            await _action(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Captures workflow events for assertions in tests.
/// </summary>
public sealed class InMemoryWorkflowEvents : IWorkflowEvents
{
    /// <summary>Gets workflow started events.</summary>
    public List<IWorkflowContext> WorkflowStarted { get; } = new();

    /// <summary>Gets workflow completed events.</summary>
    public List<IWorkflowContext> WorkflowCompleted { get; } = new();

    /// <summary>Gets workflow failed events.</summary>
    public List<(IWorkflowContext Context, Exception Exception)> WorkflowFailed { get; } = new();

    /// <summary>Gets step started events.</summary>
    public List<(IWorkflowContext Context, IStep Step)> StepStarted { get; } = new();

    /// <summary>Gets step completed events.</summary>
    public List<(IWorkflowContext Context, IStep Step)> StepCompleted { get; } = new();

    /// <summary>Gets step failed events.</summary>
    public List<(IWorkflowContext Context, IStep Step, Exception Exception)> StepFailed { get; } = new();

    /// <inheritdoc />
    public Task OnWorkflowStartedAsync(IWorkflowContext context)
    { WorkflowStarted.Add(context); return Task.CompletedTask; }

    /// <inheritdoc />
    public Task OnWorkflowCompletedAsync(IWorkflowContext context)
    { WorkflowCompleted.Add(context); return Task.CompletedTask; }

    /// <inheritdoc />
    public Task OnWorkflowFailedAsync(IWorkflowContext context, Exception exception)
    { WorkflowFailed.Add((context, exception)); return Task.CompletedTask; }

    /// <inheritdoc />
    public Task OnStepStartedAsync(IWorkflowContext context, IStep step)
    { StepStarted.Add((context, step)); return Task.CompletedTask; }

    /// <inheritdoc />
    public Task OnStepCompletedAsync(IWorkflowContext context, IStep step)
    { StepCompleted.Add((context, step)); return Task.CompletedTask; }

    /// <inheritdoc />
    public Task OnStepFailedAsync(IWorkflowContext context, IStep step, Exception exception)
    { StepFailed.Add((context, step, exception)); return Task.CompletedTask; }
}

/// <summary>
/// Builder for testing individual steps in isolation.
/// </summary>
public sealed class StepTestBuilder
{
    private readonly Dictionary<string, object?> _properties = new();
    private CancellationToken _cancellationToken;

    /// <summary>
    /// Sets a property in the context.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <param name="value">The property value.</param>
    /// <returns>This builder for chaining.</returns>
    public StepTestBuilder WithProperty(string key, object? value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the cancellation token.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>This builder for chaining.</returns>
    public StepTestBuilder WithCancellation(CancellationToken token)
    {
        _cancellationToken = token;
        return this;
    }

    /// <summary>
    /// Executes a step and returns the context.
    /// </summary>
    /// <param name="step">The step to test.</param>
    /// <returns>The workflow context after execution.</returns>
    public async Task<IWorkflowContext> ExecuteAsync(IStep step)
    {
        var context = new WorkflowContext(_cancellationToken);
        foreach (var kvp in _properties)
            context.Properties[kvp.Key] = kvp.Value;

        await step.ExecuteAsync(context).ConfigureAwait(false);
        return context;
    }

    /// <summary>
    /// Executes a typed step and returns the context.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <param name="step">The step to test.</param>
    /// <param name="data">The initial data.</param>
    /// <returns>The typed workflow context after execution.</returns>
    public async Task<IWorkflowContext<TData>> ExecuteAsync<TData>(IStep<TData> step, TData data) where TData : class
    {
        var context = new WorkflowContext<TData>(data, _cancellationToken);
        foreach (var kvp in _properties)
            context.Properties[kvp.Key] = kvp.Value;

        await step.ExecuteAsync(context).ConfigureAwait(false);
        return context;
    }
}
