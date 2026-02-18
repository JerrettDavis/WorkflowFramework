using System.Collections.Concurrent;
using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Engine;

/// <summary>
/// Thread-safe registry for field transformers. Applies transformer chains sequentially.
/// </summary>
public sealed class FieldTransformerRegistry : IFieldTransformerRegistry
{
    private readonly ConcurrentDictionary<string, IFieldTransformer> _transformers;

    /// <summary>
    /// Initializes a new instance with the given transformers.
    /// </summary>
    /// <param name="transformers">Initial transformers to register.</param>
    public FieldTransformerRegistry(IEnumerable<IFieldTransformer>? transformers = null)
    {
        _transformers = new ConcurrentDictionary<string, IFieldTransformer>(StringComparer.OrdinalIgnoreCase);
        if (transformers != null)
        {
            foreach (var t in transformers)
                _transformers.TryAdd(t.Name, t);
        }
    }

    /// <inheritdoc />
    public IFieldTransformer? Get(string name) =>
        string.IsNullOrEmpty(name) ? null : _transformers.TryGetValue(name, out var t) ? t : null;

    /// <inheritdoc />
    public string? ApplyAll(string? value, IEnumerable<TransformerRef>? transformerChain)
    {
        if (transformerChain == null)
            return value;

        foreach (var tRef in transformerChain)
        {
            var transformer = Get(tRef.Name);
            if (transformer != null)
            {
                try
                {
                    value = transformer.Transform(value, tRef.Args);
                }
                catch
                {
                    // Transformer failure: pass through unchanged
                }
            }
        }

        return value;
    }

    /// <inheritdoc />
    public void Register(IFieldTransformer transformer)
    {
        if (transformer == null) throw new ArgumentNullException(nameof(transformer));
        if (!_transformers.TryAdd(transformer.Name, transformer))
            throw new InvalidOperationException($"A transformer named '{transformer.Name}' is already registered.");
    }

    /// <inheritdoc />
    public IEnumerable<string> RegisteredNames => _transformers.Keys;
}
