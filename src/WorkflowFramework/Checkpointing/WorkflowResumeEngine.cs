namespace WorkflowFramework.Checkpointing;

/// <summary>
/// Engine that can resume a failed workflow from its last checkpoint.
/// </summary>
public sealed class WorkflowResumeEngine
{
    private readonly IWorkflowCheckpointStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowResumeEngine"/>.
    /// </summary>
    public WorkflowResumeEngine(IWorkflowCheckpointStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Resumes a workflow from its last checkpoint, skipping already-completed steps.
    /// </summary>
    /// <param name="workflow">The workflow to resume.</param>
    /// <param name="context">The workflow context to use. Properties will be restored from the checkpoint.</param>
    /// <returns>The workflow result.</returns>
    public async Task<WorkflowResult> ResumeAsync(IWorkflow workflow, IWorkflowContext context)
    {
        if (workflow == null) throw new ArgumentNullException(nameof(workflow));
        if (context == null) throw new ArgumentNullException(nameof(context));

        var checkpoint = await _store.LoadAsync(context.WorkflowId, context.CancellationToken).ConfigureAwait(false);

        if (checkpoint == null)
        {
            // No checkpoint found — run from the beginning
            return await workflow.ExecuteAsync(context).ConfigureAwait(false);
        }

        // Restore context properties from checkpoint
        context.Properties.Clear();
        foreach (var kvp in checkpoint.ContextSnapshot)
        {
            context.Properties[kvp.Key] = kvp.Value;
        }

        // Resume from the step after the last completed one
        var resumeFromIndex = checkpoint.StepIndex + 1;
        var steps = workflow.Steps;

        if (resumeFromIndex >= steps.Count)
        {
            // All steps were completed — workflow is done
            return new WorkflowResult(WorkflowStatus.Completed, context);
        }

        // Build a new workflow with only the remaining steps
        var builder = Workflow.Create(workflow.Name + "_Resumed");
        for (var i = resumeFromIndex; i < steps.Count; i++)
        {
            builder.Step(steps[i]);
        }

        // Re-apply checkpointing middleware
        builder.Use(new CheckpointingMiddleware(_store));

        var resumedWorkflow = builder.Build();
        var result = await resumedWorkflow.ExecuteAsync(context).ConfigureAwait(false);

        // Clear checkpoint on success
        if (result.IsSuccess)
        {
            await _store.ClearAsync(context.WorkflowId, context.CancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Resumes a workflow by workflow ID, using the stored checkpoint to determine where to resume.
    /// </summary>
    /// <param name="workflowId">The workflow ID to look up the checkpoint for.</param>
    /// <param name="workflow">The workflow to resume.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The workflow result.</returns>
    public async Task<WorkflowResult> ResumeAsync(string workflowId, IWorkflow workflow, CancellationToken cancellationToken = default)
    {
        if (workflowId == null) throw new ArgumentNullException(nameof(workflowId));
        if (workflow == null) throw new ArgumentNullException(nameof(workflow));

        var checkpoint = await _store.LoadAsync(workflowId, cancellationToken).ConfigureAwait(false);

        if (checkpoint == null)
        {
            var freshContext = new WorkflowContext(cancellationToken);
            return await workflow.ExecuteAsync(freshContext).ConfigureAwait(false);
        }

        // Create context and restore state
        var context = new ResumableWorkflowContext(workflowId, cancellationToken);
        foreach (var kvp in checkpoint.ContextSnapshot)
        {
            context.Properties[kvp.Key] = kvp.Value;
        }

        var resumeFromIndex = checkpoint.StepIndex + 1;
        var steps = workflow.Steps;

        if (resumeFromIndex >= steps.Count)
        {
            return new WorkflowResult(WorkflowStatus.Completed, context);
        }

        var builder = Workflow.Create(workflow.Name + "_Resumed");
        for (var i = resumeFromIndex; i < steps.Count; i++)
        {
            builder.Step(steps[i]);
        }

        builder.Use(new CheckpointingMiddleware(_store));

        var resumedWorkflow = builder.Build();
        var result = await resumedWorkflow.ExecuteAsync(context).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await _store.ClearAsync(workflowId, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}

/// <summary>
/// A workflow context that preserves a specific workflow ID (for resume scenarios).
/// </summary>
public sealed class ResumableWorkflowContext : IWorkflowContext
{
    public ResumableWorkflowContext(string workflowId, CancellationToken cancellationToken = default)
    {
        WorkflowId = workflowId;
        CorrelationId = Guid.NewGuid().ToString("N");
        CancellationToken = cancellationToken;
    }

    public string WorkflowId { get; }
    public string CorrelationId { get; }
    public CancellationToken CancellationToken { get; }
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
    public string? CurrentStepName { get; set; }
    public int CurrentStepIndex { get; set; }
    public bool IsAborted { get; set; }
    public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
}
