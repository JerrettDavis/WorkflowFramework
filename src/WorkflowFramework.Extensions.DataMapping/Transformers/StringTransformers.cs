using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Transformers;

/// <summary>
/// Transforms string values to upper case using invariant culture.
/// </summary>
public sealed class ToUpperTransformer : IFieldTransformer
{
    /// <inheritdoc />
    public string Name => "toUpper";

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null) =>
        input?.ToUpperInvariant();
}

/// <summary>
/// Transforms string values to lower case using invariant culture.
/// </summary>
public sealed class ToLowerTransformer : IFieldTransformer
{
    /// <inheritdoc />
    public string Name => "toLower";

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null) =>
        input?.ToLowerInvariant();
}

/// <summary>
/// Trims leading and trailing whitespace from string values.
/// </summary>
public sealed class TrimTransformer : IFieldTransformer
{
    /// <inheritdoc />
    public string Name => "trim";

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null) =>
        input?.Trim();
}
