namespace WorkflowFramework.Tests.Extensions;

/// <summary>Shared test helpers for extension tests.</summary>
internal class TestWorkflowContext : IWorkflowContext
{
    public string WorkflowId { get; set; } = Guid.NewGuid().ToString("N");
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public CancellationToken CancellationToken { get; set; }
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
    public string? CurrentStepName { get; set; }
    public int CurrentStepIndex { get; set; }
    public bool IsAborted { get; set; }
    public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
}

internal class TestStep : IStep
{
    private readonly Func<IWorkflowContext, Task> _action;
    public TestStep(string name, Func<IWorkflowContext, Task>? action = null)
    {
        Name = name;
        _action = action ?? (_ => Task.CompletedTask);
    }
    public string Name { get; }
    public Task ExecuteAsync(IWorkflowContext context) => _action(context);
}

internal class FailingStep : IStep
{
    public string Name => "Failing";
    public Task ExecuteAsync(IWorkflowContext context) => throw new InvalidOperationException("Step failed");
}
