using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.DependencyInjection;
using WorkflowFramework.Samples.TaskStream.Agents;
using WorkflowFramework.Samples.TaskStream.Hooks;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Sources;
using WorkflowFramework.Samples.TaskStream.Steps;
using WorkflowFramework.Samples.TaskStream.Store;
using WorkflowFramework.Samples.TaskStream.Tools;
using WorkflowFramework.Samples.TaskStream.Workflows;

namespace WorkflowFramework.Samples.TaskStream.Extensions;

/// <summary>
/// Extension methods to register all TaskStream services with DI.
/// </summary>
public static class TaskStreamServiceCollectionExtensions
{
    /// <summary>
    /// Adds all TaskStream services to the service collection.
    /// </summary>
    public static IServiceCollection AddTaskStream(
        this IServiceCollection services,
        IEnumerable<SourceMessage> sampleMessages)
    {
        // Core framework
        services.AddWorkflowFramework();

        // Sources
        var inMemorySource = new InMemoryTaskSource(sampleMessages);
        services.AddSingleton<ITaskSource>(inMemorySource);
        services.AddSingleton<ITaskSource, MockEmailTaskSource>();
        services.AddSingleton<AggregateTaskSource>(sp =>
            new AggregateTaskSource(sp.GetServices<ITaskSource>()));

        // Store
        services.AddSingleton<ITodoStore, InMemoryTodoStore>();

        // Hooks
        services.AddSingleton<ITodoHook, ConsoleHook>();

        // Tools
        services.AddSingleton<IAgentTool, WebSearchTool>();
        services.AddSingleton<IAgentTool, CalendarTool>();
        services.AddSingleton<IAgentTool, LocationTool>();
        services.AddSingleton<IAgentTool, DeploymentTool>();
        services.AddSingleton<IAgentTool, FileSystemTool>();

        // Agent provider
        services.AddSingleton<IAgentProvider>(sp =>
            new TaskStreamAgentProvider(sp.GetServices<IAgentTool>()));

        // Steps
        services.AddTransient<CollectSourcesStep>();
        services.AddTransient<NormalizeInputStep>();
        services.AddTransient<ExtractTodosStep>();
        services.AddTransient<ValidateAndDeduplicateStep>();
        services.AddTransient<PersistTodosStep>();
        services.AddTransient<TriageStep>();
        services.AddTransient<AgentExecutionStep>();
        services.AddTransient<EnrichHumanTaskStep>();
        services.AddTransient<AggregateResultsStep>();
        services.AddTransient<FormatMarkdownStep>();

        // Orchestrator
        services.AddTransient<TaskStreamOrchestrator>();

        return services;
    }
}
