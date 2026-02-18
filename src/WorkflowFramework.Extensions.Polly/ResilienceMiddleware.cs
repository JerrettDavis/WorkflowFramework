using Polly;

namespace WorkflowFramework.Extensions.Polly;

/// <summary>
/// Middleware that wraps step execution with a Polly <see cref="ResiliencePipeline"/>.
/// </summary>
public sealed class ResilienceMiddleware : IWorkflowMiddleware
{
    private readonly ResiliencePipeline _pipeline;

    /// <summary>
    /// Initializes a new instance of <see cref="ResilienceMiddleware"/>.
    /// </summary>
    /// <param name="pipeline">The Polly resilience pipeline to use.</param>
    public ResilienceMiddleware(ResiliencePipeline pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        await _pipeline.ExecuteAsync(
            static async (state, ct) =>
            {
                await state.next(state.context).ConfigureAwait(false);
            },
            (context, next),
            context.CancellationToken).ConfigureAwait(false);
    }
}
