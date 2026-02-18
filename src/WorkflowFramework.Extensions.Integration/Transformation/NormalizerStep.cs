namespace WorkflowFramework.Extensions.Integration.Transformation;

/// <summary>
/// Routes different data formats through format-specific translators to produce a canonical model.
/// </summary>
public sealed class NormalizerStep : IStep
{
    private readonly Func<IWorkflowContext, string> _formatDetector;
    private readonly IDictionary<string, IStep> _translators;
    private readonly IStep? _defaultTranslator;

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
        _translators = translators ?? throw new ArgumentNullException(nameof(translators));
        _defaultTranslator = defaultTranslator;
    }

    /// <inheritdoc />
    public string Name => "Normalizer";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var format = _formatDetector(context);

        if (_translators.TryGetValue(format, out var translator))
        {
            await translator.ExecuteAsync(context).ConfigureAwait(false);
        }
        else if (_defaultTranslator != null)
        {
            await _defaultTranslator.ExecuteAsync(context).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException($"No translator found for format '{format}' and no default translator configured.");
        }
    }
}
