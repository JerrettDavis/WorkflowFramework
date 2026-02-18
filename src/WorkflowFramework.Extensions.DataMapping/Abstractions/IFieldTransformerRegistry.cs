namespace WorkflowFramework.Extensions.DataMapping.Abstractions;

/// <summary>
/// Registry for discovering and applying field transformers.
/// </summary>
public interface IFieldTransformerRegistry
{
    /// <summary>
    /// Gets a transformer by name.
    /// </summary>
    /// <param name="name">The transformer name (case-insensitive).</param>
    /// <returns>The transformer instance, or null if not found.</returns>
    IFieldTransformer? Get(string name);

    /// <summary>
    /// Applies a chain of transformers to a value sequentially.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <param name="transformerChain">The transformers to apply in order.</param>
    /// <returns>The transformed value.</returns>
    string? ApplyAll(string? value, IEnumerable<TransformerRef>? transformerChain);

    /// <summary>
    /// Registers a transformer.
    /// </summary>
    /// <param name="transformer">The transformer to register.</param>
    void Register(IFieldTransformer transformer);

    /// <summary>
    /// Gets all registered transformer names.
    /// </summary>
    IEnumerable<string> RegisteredNames { get; }
}
