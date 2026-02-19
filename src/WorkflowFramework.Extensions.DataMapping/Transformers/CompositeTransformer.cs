using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Transformers;

/// <summary>
/// Chains multiple transformers together. This is used internally by the registry;
/// for most use cases, simply provide multiple <see cref="TransformerRef"/> in a field mapping.
/// </summary>
public sealed class CompositeTransformer : IFieldTransformer
{
    private readonly IReadOnlyList<IFieldTransformer> _inner;

    /// <summary>
    /// Initializes a new composite transformer.
    /// </summary>
    /// <param name="name">The name for this composite.</param>
    /// <param name="transformers">The transformers to chain.</param>
    public CompositeTransformer(string name, IReadOnlyList<IFieldTransformer> transformers)
    {
        Name = name;
        _inner = transformers;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null)
    {
        var value = input;
        foreach (var t in _inner)
            value = t.Transform(value, args);
        return value;
    }
}
