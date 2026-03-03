using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Manages workflow execution runs for the dashboard API.
/// </summary>
public sealed class WorkflowRunService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkflowExecutionNotifier _notifier;
    private readonly ConcurrentDictionary<string, RunState> _runs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();

    public WorkflowRunService(
        IServiceScopeFactory scopeFactory,
        WorkflowExecutionNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
    }

    /// <summary>Starts a workflow run with real async execution.</summary>
    public async Task<RunSummary?> StartRunAsync(
        string workflowId,
        Dictionary<string, JsonElement>? inputs = null,
        string? source = null,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionStore>();
        var workflow = await store.GetByIdAsync(workflowId, ct);
        if (workflow is null) return null;

        var normalizedInputs = NormalizeInputs(inputs);

        var run = new RunState
        {
            RunId = Guid.NewGuid().ToString("N"),
            WorkflowId = workflowId,
            WorkflowName = workflow.Definition.Name,
            Source = source,
            Inputs = normalizedInputs.ToDictionary(kvp => kvp.Key, kvp => ToInputString(kvp.Value)),
            StepResults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Status = "Running",
            StartedAt = DateTimeOffset.UtcNow
        };
        _runs[run.RunId] = run;

        var cts = new CancellationTokenSource();
        _cancellations[run.RunId] = cts;

        // Fire-and-forget execution (tracked by run state)
        _ = ExecuteWorkflowAsync(run, workflow.Definition, normalizedInputs, cts.Token);

        return ToSummary(run);
    }

    private async Task ExecuteWorkflowAsync(
        RunState run,
        Serialization.WorkflowDefinitionDto definition,
        Dictionary<string, object?> inputs,
        CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var compiler = scope.ServiceProvider.GetRequiredService<WorkflowDefinitionCompiler>();
            var trackedEvents = new RunStateTrackingEvents(run);
            var compositeEvents = new CompositeWorkflowEvents(_notifier, trackedEvents);
            var compiledWorkflow = compiler.Compile(definition, compositeEvents);
            var context = new DashboardWorkflowContext(run.RunId, ct);
            context.Properties["WorkflowName"] = definition.Name;
            context.Properties["Run.Source"] = run.Source ?? "manual";
            ApplyInputsToContext(context, inputs);

            var result = await compiledWorkflow.ExecuteAsync(context);

            lock (run.Gate)
            {
                run.Status = result.Status == WorkflowStatus.Completed ? "Completed" : "Failed";
                run.CompletedAt = DateTimeOffset.UtcNow;
                if (result.Errors.Count > 0)
                    run.Error = string.Join("; ", result.Errors.Select(e => $"[{e.StepName}] {e.Exception.Message}"));
            }

            foreach (var pair in context.Properties.Where(p => p.Key.Contains('.')))
            {
                lock (run.Gate)
                {
                    run.StepResults ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    run.StepResults[pair.Key] = pair.Value?.ToString() ?? "";
                }
            }
        }
        catch (OperationCanceledException)
        {
            lock (run.Gate)
            {
                run.Status = "Cancelled";
                run.CompletedAt = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            lock (run.Gate)
            {
                run.Status = "Failed";
                run.Error = ex.Message;
                run.CompletedAt = DateTimeOffset.UtcNow;
            }
        }
        finally
        {
            _cancellations.TryRemove(run.RunId, out _);
        }
    }

    /// <summary>Lists recent runs.</summary>
    public Task<IReadOnlyList<RunSummary>> GetRunsAsync(int? limit = null, CancellationToken ct = default)
    {
        var runs = _runs.Values
            .OrderByDescending(r => r.StartedAt)
            .Take(limit ?? 100)
            .Select(ToSummary)
            .ToList();
        return Task.FromResult<IReadOnlyList<RunSummary>>(runs);
    }

    /// <summary>Gets a specific run by ID.</summary>
    public Task<RunSummary?> GetRunAsync(string runId, CancellationToken ct = default)
    {
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run is null ? null : ToSummary(run));
    }

    /// <summary>Cancels a running workflow.</summary>
    public Task<bool> CancelRunAsync(string runId, CancellationToken ct = default)
    {
        if (!_runs.TryGetValue(runId, out var run))
            return Task.FromResult(false);

        if (run.Status == "Running")
        {
            if (_cancellations.TryRemove(runId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
            lock (run.Gate)
            {
                run.Status = "Cancelled";
                run.CompletedAt = DateTimeOffset.UtcNow;
            }
        }
        return Task.FromResult(true);
    }

    private static RunSummary ToSummary(RunState state)
    {
        lock (state.Gate)
        {
            return new RunSummary
            {
                RunId = state.RunId,
                WorkflowId = state.WorkflowId,
                WorkflowName = state.WorkflowName,
                Source = state.Source,
                Status = state.Status,
                StartedAt = state.StartedAt,
                CompletedAt = state.CompletedAt,
                Inputs = state.Inputs is null ? null : new Dictionary<string, string>(state.Inputs, StringComparer.OrdinalIgnoreCase),
                StepResults = state.StepResults is null ? null : new Dictionary<string, string>(state.StepResults, StringComparer.OrdinalIgnoreCase),
                Error = state.Error
            };
        }
    }

    private static Dictionary<string, object?> NormalizeInputs(Dictionary<string, JsonElement>? inputs)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (inputs is null)
            return normalized;

        foreach (var (key, value) in inputs)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            normalized[key] = ConvertJsonElement(value);
        }

        return normalized;
    }

    private static void ApplyInputsToContext(DashboardWorkflowContext context, Dictionary<string, object?> inputs)
    {
        if (inputs.Count == 0)
            return;

        context.Properties["Input"] = inputs;

        foreach (var (key, value) in inputs)
        {
            context.Properties[key] = value;
            FlattenValue(context.Properties, $"Input.{key}", value);
        }
    }

    private static void FlattenValue(IDictionary<string, object?> target, string path, object? value)
    {
        target[path] = value;

        if (value is Dictionary<string, object?> map)
        {
            foreach (var (childKey, childValue) in map)
            {
                if (string.IsNullOrWhiteSpace(childKey))
                    continue;

                FlattenValue(target, $"{path}.{childKey}", childValue);
            }
            return;
        }

        if (value is List<object?> list)
            target[$"{path}.Count"] = list.Count;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ConvertNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => element
                .EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            _ => null
        };
    }

    private static object ConvertNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var i64))
            return i64;
        if (element.TryGetDecimal(out var dec))
            return dec;
        return element.GetDouble();
    }

    private static string ToInputString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            Dictionary<string, object?> or List<object?> => JsonSerializer.Serialize(value),
            _ => Convert.ToString(value) ?? string.Empty
        };
    }

    private sealed class RunState
    {
        public object Gate { get; } = new();
        public string RunId { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public Dictionary<string, string>? Inputs { get; set; }
        public Dictionary<string, string>? StepResults { get; set; }
        public string? Error { get; set; }
    }

    private sealed class RunStateTrackingEvents(RunState run) : WorkflowEventsBase
    {
        public override Task OnWorkflowStartedAsync(IWorkflowContext context)
        {
            lock (run.Gate)
            {
                run.Status = "Running";
            }
            return Task.CompletedTask;
        }

        public override Task OnStepStartedAsync(IWorkflowContext context, IStep step)
        {
            lock (run.Gate)
            {
                run.StepResults ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                run.StepResults[$"{step.Name}.Status"] = "Running";
                run.StepResults[$"{step.Name}.StartedAt"] = DateTimeOffset.UtcNow.ToString("O");
            }
            return Task.CompletedTask;
        }

        public override Task OnStepCompletedAsync(IWorkflowContext context, IStep step)
        {
            lock (run.Gate)
            {
                run.StepResults ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                run.StepResults[$"{step.Name}.Status"] = "Completed";
                if (context.Properties.TryGetValue($"{step.Name}.Output", out var output))
                    run.StepResults[$"{step.Name}.Output"] = output?.ToString() ?? "";
            }
            return Task.CompletedTask;
        }

        public override Task OnStepFailedAsync(IWorkflowContext context, IStep step, Exception exception)
        {
            lock (run.Gate)
            {
                run.StepResults ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                run.StepResults[$"{step.Name}.Status"] = "Failed";
                run.StepResults[$"{step.Name}.Error"] = exception.Message;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CompositeWorkflowEvents(IWorkflowEvents primary, IWorkflowEvents secondary) : WorkflowEventsBase
    {
        public override async Task OnWorkflowStartedAsync(IWorkflowContext context)
        {
            await primary.OnWorkflowStartedAsync(context);
            await secondary.OnWorkflowStartedAsync(context);
        }

        public override async Task OnWorkflowCompletedAsync(IWorkflowContext context)
        {
            await primary.OnWorkflowCompletedAsync(context);
            await secondary.OnWorkflowCompletedAsync(context);
        }

        public override async Task OnWorkflowFailedAsync(IWorkflowContext context, Exception exception)
        {
            await primary.OnWorkflowFailedAsync(context, exception);
            await secondary.OnWorkflowFailedAsync(context, exception);
        }

        public override async Task OnStepStartedAsync(IWorkflowContext context, IStep step)
        {
            await primary.OnStepStartedAsync(context, step);
            await secondary.OnStepStartedAsync(context, step);
        }

        public override async Task OnStepCompletedAsync(IWorkflowContext context, IStep step)
        {
            await primary.OnStepCompletedAsync(context, step);
            await secondary.OnStepCompletedAsync(context, step);
        }

        public override async Task OnStepFailedAsync(IWorkflowContext context, IStep step, Exception exception)
        {
            await primary.OnStepFailedAsync(context, step, exception);
            await secondary.OnStepFailedAsync(context, step, exception);
        }
    }
}
