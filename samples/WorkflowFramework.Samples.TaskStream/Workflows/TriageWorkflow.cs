using WorkflowFramework.Samples.TaskStream.Steps;

namespace WorkflowFramework.Samples.TaskStream.Workflows;

/// <summary>
/// Workflow: Triage → Parallel(AgentExecution, EnrichHumanTasks)
/// </summary>
public static class TriageWorkflow
{
    /// <summary>Builds the triage workflow with the given steps.</summary>
    public static IWorkflow Build(
        TriageStep triage,
        AgentExecutionStep agentExec,
        EnrichHumanTaskStep enrich)
    {
        return Workflow.Create("Triage")
            .Step(triage)
            .Parallel(p => p
                .Step(agentExec)
                .Step(enrich))
            .Build();
    }
}
