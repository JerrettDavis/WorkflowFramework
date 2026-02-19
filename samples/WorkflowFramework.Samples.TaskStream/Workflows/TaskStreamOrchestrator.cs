using WorkflowFramework.Builder;
using WorkflowFramework.Samples.TaskStream.Steps;

namespace WorkflowFramework.Samples.TaskStream.Workflows;

/// <summary>
/// The main orchestrator that chains: Extraction → Triage → Report.
/// </summary>
public sealed class TaskStreamOrchestrator
{
    private readonly IWorkflow _workflow;

    /// <summary>Initializes a new instance with all required steps.</summary>
    public TaskStreamOrchestrator(
        CollectSourcesStep collect,
        NormalizeInputStep normalize,
        ExtractTodosStep extract,
        ValidateAndDeduplicateStep validate,
        PersistTodosStep persist,
        TriageStep triage,
        AgentExecutionStep agentExec,
        EnrichHumanTaskStep enrich,
        AggregateResultsStep aggregate,
        FormatMarkdownStep format)
    {
        var extraction = ExtractionWorkflow.Build(collect, normalize, extract, validate, persist);
        var triageWf = TriageWorkflow.Build(triage, agentExec, enrich);
        var report = ReportWorkflow.Build(aggregate, format);

        _workflow = Workflow.Create("TaskStream")
            .SubWorkflow(extraction)
            .SubWorkflow(triageWf)
            .SubWorkflow(report)
            .Build();
    }

    /// <summary>Executes the full TaskStream pipeline.</summary>
    public async Task<WorkflowResult> ExecuteAsync(CancellationToken ct = default)
    {
        var context = new WorkflowContext(ct);
        return await _workflow.ExecuteAsync(context);
    }
}
