using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Transformation;

/// <summary>
/// Transforms workflow data from one schema/format to another using a configurable mapper.
/// </summary>
/// <typeparam name="TIn">The input type.</typeparam>
/// <typeparam name="TOut">The output type.</typeparam>
public sealed class MessageTranslatorStep<TIn, TOut> : IStep
{
    private readonly IMessageTranslator<TIn, TOut> _translator;
    private readonly Func<IWorkflowContext, TIn> _inputSelector;
    private readonly string _outputKey;

    /// <summary>
    /// Initializes a new instance of <see cref="MessageTranslatorStep{TIn, TOut}"/>.
    /// </summary>
    /// <param name="translator">The translator implementation.</param>
    /// <param name="inputSelector">Function to extract the input from context.</param>
    /// <param name="outputKey">The property key to store the translated output.</param>
    public MessageTranslatorStep(
        IMessageTranslator<TIn, TOut> translator,
        Func<IWorkflowContext, TIn> inputSelector,
        string outputKey = "__TranslatedOutput")
    {
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _inputSelector = inputSelector ?? throw new ArgumentNullException(nameof(inputSelector));
        _outputKey = outputKey;
    }

    /// <inheritdoc />
    public string Name => "MessageTranslator";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var input = _inputSelector(context);
        var output = await _translator.TranslateAsync(input, context.CancellationToken).ConfigureAwait(false);
        context.Properties[_outputKey] = output;
    }
}
