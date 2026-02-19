#pragma warning disable SKEXP0070
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.HumanTasks;
using WorkflowFramework.Samples.VoiceWorkflows.Hooks;
using WorkflowFramework.Samples.VoiceWorkflows.Models;
using WorkflowFramework.Samples.VoiceWorkflows.Tools;

namespace WorkflowFramework.Samples.VoiceWorkflows.Extensions;

/// <summary>DI registration for voice workflows.</summary>
public static class VoiceWorkflowServiceCollectionExtensions
{
    public static IServiceCollection AddVoiceWorkflows(this IServiceCollection services, IEnumerable<string>? args = null)
    {
        services.AddWorkflowFramework();

        // Tool providers
        services.AddSingleton<IToolProvider>(new WhisperToolProvider(new WhisperOptions()));
        services.AddSingleton<IToolProvider, SpeakerDiarizationToolProvider>();
        services.AddSingleton<IToolProvider, AudioToolProvider>();
        services.AddSingleton<IToolProvider, TextToolProvider>();

        // Tool registry
        services.AddSingleton(sp =>
        {
            var registry = new ToolRegistry();
            foreach (var provider in sp.GetServices<IToolProvider>())
                registry.Register(provider);
            return registry;
        });

        // Human task inbox
        services.AddSingleton<ITaskInbox, SimulatedHumanTaskInbox>();

        // Hooks
        services.AddSingleton<IAgentHook, ConsoleLoggingHook>();
        services.AddSingleton(sp =>
        {
            var pipeline = new HookPipeline(sp.GetServices<IAgentHook>());
            return pipeline;
        });

        // Checkpoint store
        services.AddSingleton<ICheckpointStore, InMemoryCheckpointStore>();

        // Agent provider
        var argsList = args?.ToList() ?? [];
        var useOllama = Environment.GetEnvironmentVariable("USE_OLLAMA")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
                        || argsList.Contains("--use-ollama", StringComparer.OrdinalIgnoreCase);
        if (useOllama)
        {
            services.AddSingleton<IAgentProvider>(new OllamaAgentProvider(new OllamaOptions()));
        }
        else
        {
            services.AddSingleton<IAgentProvider, EchoAgentProvider>();
        }

        return services;
    }
}
