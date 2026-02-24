#if NET8_0_OR_GREATER
using Microsoft.Extensions.Hosting;

namespace WorkflowFramework.Triggers;

/// <summary>
/// Hosted service that manages active triggers for all workflows.
/// </summary>
public sealed class WorkflowTriggerService : IHostedService, IAsyncDisposable
{
    private readonly ITriggerSourceFactory _factory;
    private readonly Dictionary<string, List<(TriggerDefinition Definition, ITriggerSource Source)>> _activeTriggers = new();
    private readonly object _lock = new();

    public WorkflowTriggerService(ITriggerSourceFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Activates triggers for a workflow.
    /// </summary>
    public async Task ActivateTriggersAsync(
        string workflowId,
        IEnumerable<TriggerDefinition> triggers,
        Func<TriggerEvent, Task<string>> onTriggered,
        CancellationToken ct = default)
    {
        if (workflowId is null) throw new ArgumentNullException(nameof(workflowId));
        if (triggers is null) throw new ArgumentNullException(nameof(triggers));
        if (onTriggered is null) throw new ArgumentNullException(nameof(onTriggered));

        // Deactivate existing triggers first
        await DeactivateTriggersAsync(workflowId, ct).ConfigureAwait(false);

        var active = new List<(TriggerDefinition, ITriggerSource)>();

        foreach (var def in triggers)
        {
            if (!def.Enabled) continue;

            var source = _factory.Create(def);
            var context = new TriggerContext
            {
                WorkflowId = workflowId,
                Configuration = def.Configuration,
                OnTriggered = onTriggered
            };

            await source.StartAsync(context, ct).ConfigureAwait(false);
            active.Add((def, source));
        }

        if (active.Count > 0)
        {
            lock (_lock)
            {
                _activeTriggers[workflowId] = active;
            }
        }
    }

    /// <summary>
    /// Deactivates all triggers for a workflow.
    /// </summary>
    public async Task DeactivateTriggersAsync(string workflowId, CancellationToken ct = default)
    {
        List<(TriggerDefinition, ITriggerSource)>? existing;
        lock (_lock)
        {
            if (!_activeTriggers.Remove(workflowId, out existing))
                return;
        }

        foreach (var (_, source) in existing)
        {
            try
            {
                await source.StopAsync(ct).ConfigureAwait(false);
                await source.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    /// <summary>
    /// Gets the count of active triggers per workflow.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetActiveTriggerCounts()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, int>();
            foreach (var kvp in _activeTriggers)
                result[kvp.Key] = kvp.Value.Count;
            return result;
        }
    }

    /// <summary>
    /// Gets all active trigger sources for a workflow.
    /// </summary>
    public IReadOnlyList<ITriggerSource> GetActiveSources(string workflowId)
    {
        lock (_lock)
        {
            if (_activeTriggers.TryGetValue(workflowId, out var list))
                return list.Select(x => x.Source).ToList();
            return Array.Empty<ITriggerSource>();
        }
    }

    Task IHostedService.StartAsync(CancellationToken ct) => Task.CompletedTask;

    async Task IHostedService.StopAsync(CancellationToken ct)
    {
        List<string> workflowIds;
        lock (_lock)
        {
            workflowIds = new List<string>(_activeTriggers.Keys);
        }

        foreach (var id in workflowIds)
        {
            await DeactivateTriggersAsync(id, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        List<string> workflowIds;
        lock (_lock)
        {
            workflowIds = new List<string>(_activeTriggers.Keys);
        }

        foreach (var id in workflowIds)
        {
            await DeactivateTriggersAsync(id).ConfigureAwait(false);
        }
    }
}
#endif
