#if !NETSTANDARD2_0
namespace WorkflowFramework.Triggers;

/// <summary>
/// Defines a source that can trigger workflow execution.
/// </summary>
public interface ITriggerSource : IAsyncDisposable
{
    /// <summary>Unique trigger type identifier (e.g., "schedule", "filewatch", "webhook", "queue").</summary>
    string Type { get; }

    /// <summary>Display name for UI.</summary>
    string DisplayName { get; }

    /// <summary>Starts listening/polling for trigger events.</summary>
    Task StartAsync(TriggerContext context, CancellationToken ct = default);

    /// <summary>Stops the trigger.</summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Whether the trigger is currently active.</summary>
    bool IsRunning { get; }
}

#endif
