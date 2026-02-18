using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Middleware that caches step results to avoid re-execution.
/// Uses step name as cache key by default.
/// </summary>
public sealed class CachingMiddleware : IWorkflowMiddleware
{
    private readonly ConcurrentDictionary<string, bool> _executedSteps = new();

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        var cacheKey = $"{context.WorkflowId}:{step.Name}";
        if (!_executedSteps.TryAdd(cacheKey, true))
        {
            // Already executed, skip
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void Clear() => _executedSteps.Clear();
}

/// <summary>
/// Middleware that prevents duplicate step execution (idempotency).
/// Steps that have already completed in this workflow run are skipped.
/// </summary>
public sealed class IdempotencyMiddleware : IWorkflowMiddleware
{
    private readonly ConcurrentDictionary<string, bool> _completedSteps = new();

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        var key = $"{context.WorkflowId}:{step.Name}:{context.CurrentStepIndex}";
        if (_completedSteps.ContainsKey(key))
            return;

        await next(context).ConfigureAwait(false);
        _completedSteps.TryAdd(key, true);
    }
}

/// <summary>
/// Middleware that validates step inputs before execution.
/// </summary>
public sealed class ValidationMiddleware : IWorkflowMiddleware
{
    private readonly Func<IWorkflowContext, IStep, Task<bool>> _validator;

    /// <summary>
    /// Initializes a new instance of <see cref="ValidationMiddleware"/>.
    /// </summary>
    /// <param name="validator">A function that validates the context and step. Returns true if valid.</param>
    public ValidationMiddleware(Func<IWorkflowContext, IStep, Task<bool>> validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        var isValid = await _validator(context, step).ConfigureAwait(false);
        if (!isValid)
        {
            throw new InvalidOperationException($"Validation failed for step '{step.Name}'.");
        }

        await next(context).ConfigureAwait(false);
    }
}
