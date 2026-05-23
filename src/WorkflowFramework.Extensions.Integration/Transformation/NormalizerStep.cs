using PatternKit.Messaging.Transformation;

namespace WorkflowFramework.Extensions.Integration.Transformation;

/// <summary>
/// Routes different data formats through format-specific translators to produce a canonical model.
/// Delegates to <see cref="KeyedNormalizer{TKey,TRaw,TCanonical}"/> for O(1) keyed dispatch.
/// </summary>
public sealed class NormalizerStep : IStep
{
    private readonly Func<IWorkflowContext, string> _formatDetector;
    private readonly KeyedNormalizer<string, IWorkflowContext, IWorkflowContext>? _normalizer;

    /// <summary>
    /// Initializes a new instance of <see cref="NormalizerStep"/>.
    /// </summary>
    /// <param name="formatDetector">Function to detect the format of the incoming data.</param>
    /// <param name="translators">Map of format names to translator steps.</param>
    /// <param name="defaultTranslator">Optional default translator for unknown formats.</param>
    public NormalizerStep(
        Func<IWorkflowContext, string> formatDetector,
        IDictionary<string, IStep> translators,
        IStep? defaultTranslator = null)
    {
        _formatDetector = formatDetector ?? throw new ArgumentNullException(nameof(formatDetector));
        _ = translators ?? throw new ArgumentNullException(nameof(translators));

        var builder = KeyedNormalizer<string, IWorkflowContext, IWorkflowContext>.Create("Normalizer");

        foreach (var kvp in translators)
        {
            var key = kvp.Key;
            var captured = kvp.Value;
            builder.When(key, async (ctx, ct) =>
            {
                await captured.ExecuteAsync(ctx).ConfigureAwait(false);
                return ctx;
            });
        }

        if (defaultTranslator is not null)
        {
            var capturedDefault = defaultTranslator;
            builder.Default(async (ctx, ct) =>
            {
                await capturedDefault.ExecuteAsync(ctx).ConfigureAwait(false);
                return ctx;
            });
        }

        // Build only when there is at least one handler or a default; otherwise keep null
        // so the empty-dict case falls through to our own exception with the original message.
        if (translators.Count > 0 || defaultTranslator is not null)
            _normalizer = builder.Build();
    }

    /// <inheritdoc />
    public string Name => "Normalizer";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var format = _formatDetector(context);

        if (_normalizer is null)
        {
            // No handlers registered and no default — preserve the original error contract.
            throw new InvalidOperationException(
                $"No translator found for format '{format}' and no default translator configured.");
        }

        try
        {
            await _normalizer.NormalizeAsync(format, context, context.CancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            // PatternKit throws KeyNotFoundException on a miss; wrap to preserve the original
            // InvalidOperationException contract that characterization tests assert on.
            throw new InvalidOperationException(
                $"No translator found for format '{format}' and no default translator configured.");
        }
    }
}
