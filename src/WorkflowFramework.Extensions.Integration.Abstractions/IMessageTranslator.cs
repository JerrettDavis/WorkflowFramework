namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Translates a message from one format/schema to another.
/// </summary>
/// <typeparam name="TIn">The input message type.</typeparam>
/// <typeparam name="TOut">The output message type.</typeparam>
public interface IMessageTranslator<in TIn, TOut>
{
    /// <summary>
    /// Translates a message from input format to output format.
    /// </summary>
    /// <param name="input">The input message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The translated message.</returns>
    Task<TOut> TranslateAsync(TIn input, CancellationToken cancellationToken = default);
}
