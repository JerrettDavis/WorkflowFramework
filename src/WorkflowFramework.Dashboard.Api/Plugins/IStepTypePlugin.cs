using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Plugins;

/// <summary>
/// Interface for step type plugins that extend the workflow engine.
/// </summary>
public interface IStepTypePlugin
{
    /// <summary>Unique plugin ID.</summary>
    string Id { get; }

    /// <summary>Display name.</summary>
    string Name { get; }

    /// <summary>Plugin version.</summary>
    string Version { get; }

    /// <summary>Step types provided by this plugin.</summary>
    IReadOnlyList<StepTypeInfo> StepTypes { get; }

    /// <summary>Called during initialization.</summary>
    Task InitializeAsync(IServiceProvider services, CancellationToken ct = default);

    /// <summary>Creates a step executor for the given step type and config.</summary>
    IStep? CreateStep(string stepType, string stepName, Dictionary<string, string>? config);
}
