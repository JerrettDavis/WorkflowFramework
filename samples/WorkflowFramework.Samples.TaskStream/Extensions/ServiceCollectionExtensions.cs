#pragma warning disable SKEXP0070
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.DependencyInjection;
#pragma warning disable CA1860 // Avoid using 'Enumerable.Any()' extension method
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
        IEnumerable<SourceMessage> sampleMessages,
        IEnumerable<string>? args = null)
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

        // Agent provider — use SK, Ollama, or rule-based mock
        var argsList = args?.ToList() ?? [];
        var useOllama = Environment.GetEnvironmentVariable("USE_OLLAMA")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
                        || argsList.Contains("--use-ollama", StringComparer.OrdinalIgnoreCase);
        var useSemanticKernel = Environment.GetEnvironmentVariable("USE_SEMANTIC_KERNEL")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
                                || argsList.Contains("--use-sk", StringComparer.OrdinalIgnoreCase);
        if (useSemanticKernel)
        {
            services.AddSingleton<IAgentProvider>(sp =>
            {
                var tools = sp.GetServices<IAgentTool>();
                var plugin = new TaskStreamPlugin(tools);

                var builder = Kernel.CreateBuilder();
                builder.AddOllamaChatCompletion("qwen3:30b-instruct", new Uri("http://localhost:11434"));
                builder.Plugins.AddFromObject(plugin, "TaskStream");
                var kernel = builder.Build();

                return new SemanticKernelAgentProvider(kernel);
            });
        }
        else if (useOllama)
        {
            services.AddSingleton<IAgentProvider>(new OllamaAgentProvider(new OllamaOptions()));
        }
        else
        {
            services.AddSingleton<IAgentProvider>(sp =>
                new TaskStreamAgentProvider(sp.GetServices<IAgentTool>()));
        }

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
