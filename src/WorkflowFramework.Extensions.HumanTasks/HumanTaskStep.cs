namespace WorkflowFramework.Extensions.HumanTasks;

/// <summary>
/// A workflow step that creates a human task and waits for its completion.
/// </summary>
public sealed class HumanTaskStep : IStep
{
    private readonly ITaskInbox _inbox;
    private readonly HumanTaskOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="HumanTaskStep"/>.
    /// </summary>
    public HumanTaskStep(ITaskInbox inbox, HumanTaskOptions options)
    {
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Name => _options.StepName ?? $"HumanTask({_options.Title})";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var task = new HumanTask
        {
            WorkflowId = context.WorkflowId,
            Title = _options.Title,
            Description = _options.Description,
            Assignee = _options.Assignee,
            DueDate = _options.DueDate,
            Escalation = _options.Escalation
        };

        await _inbox.CreateTaskAsync(task, context.CancellationToken).ConfigureAwait(false);
        context.Properties[$"{Name}.TaskId"] = task.Id;

        var completed = await _inbox.WaitForCompletionAsync(task.Id, _options.Timeout, context.CancellationToken).ConfigureAwait(false);
        context.Properties[$"{Name}.Outcome"] = completed.Outcome;
        context.Properties[$"{Name}.Status"] = completed.Status.ToString();
    }
}

/// <summary>
/// Options for configuring a human task step.
/// </summary>
public sealed class HumanTaskOptions
{
    /// <summary>Gets or sets the step name.</summary>
    public string? StepName { get; set; }

    /// <summary>Gets or sets the task title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the task description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the assignee.</summary>
    public string Assignee { get; set; } = string.Empty;

    /// <summary>Gets or sets the due date.</summary>
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>Gets or sets the timeout for waiting on task completion.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Gets or sets the escalation rules.</summary>
    public EscalationRule? Escalation { get; set; }
}

/// <summary>
/// A step that requires approval from one or more approvers.
/// </summary>
public sealed class ApprovalStep : IStep
{
    private readonly ITaskInbox _inbox;
    private readonly ApprovalOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="ApprovalStep"/>.
    /// </summary>
    public ApprovalStep(ITaskInbox inbox, ApprovalOptions options)
    {
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Name => _options.StepName ?? "Approval";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var approved = true;

        if (_options.Mode == ApprovalMode.Parallel)
        {
            var tasks = new List<Task<HumanTask>>();
            foreach (var approver in _options.Approvers)
            {
                var task = new HumanTask
                {
                    WorkflowId = context.WorkflowId,
                    Title = _options.Title,
                    Assignee = approver
                };
                await _inbox.CreateTaskAsync(task, context.CancellationToken).ConfigureAwait(false);
                tasks.Add(_inbox.WaitForCompletionAsync(task.Id, _options.Timeout, context.CancellationToken));
            }
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            approved = results.All(t => t.Status == HumanTaskStatus.Approved);
        }
        else // Sequential
        {
            foreach (var approver in _options.Approvers)
            {
                var task = new HumanTask
                {
                    WorkflowId = context.WorkflowId,
                    Title = _options.Title,
                    Assignee = approver
                };
                await _inbox.CreateTaskAsync(task, context.CancellationToken).ConfigureAwait(false);
                var result = await _inbox.WaitForCompletionAsync(task.Id, _options.Timeout, context.CancellationToken).ConfigureAwait(false);
                if (result.Status != HumanTaskStatus.Approved)
                {
                    approved = false;
                    break;
                }
            }
        }

        context.Properties[$"{Name}.Approved"] = approved;
    }
}

/// <summary>
/// Approval modes.
/// </summary>
public enum ApprovalMode
{
    /// <summary>Approvers are asked sequentially; first rejection stops the chain.</summary>
    Sequential,
    /// <summary>All approvers are asked in parallel.</summary>
    Parallel
}

/// <summary>
/// Options for configuring an approval step.
/// </summary>
public sealed class ApprovalOptions
{
    /// <summary>Gets or sets the step name.</summary>
    public string? StepName { get; set; }

    /// <summary>Gets or sets the approval title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of approvers.</summary>
    public IList<string> Approvers { get; set; } = new List<string>();

    /// <summary>Gets or sets the approval mode.</summary>
    public ApprovalMode Mode { get; set; } = ApprovalMode.Sequential;

    /// <summary>Gets or sets the timeout for each approval.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(24);
}
