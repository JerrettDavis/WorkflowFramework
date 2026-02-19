using WorkflowFramework.Samples.TaskStream.Steps;

namespace WorkflowFramework.Samples.TaskStream.Workflows;

/// <summary>
/// Workflow: CollectSources → Normalize → Extract → Validate → Persist
/// </summary>
public static class ExtractionWorkflow
{
    /// <summary>Builds the extraction workflow with the given steps.</summary>
    public static IWorkflow Build(
        CollectSourcesStep collect,
        NormalizeInputStep normalize,
        ExtractTodosStep extract,
        ValidateAndDeduplicateStep validate,
        PersistTodosStep persist)
    {
        return Workflow.Create("Extraction")
            .Step(collect)
            .Step(normalize)
            .Step(extract)
            .Step(validate)
            .Step(persist)
            .Build();
    }
}
