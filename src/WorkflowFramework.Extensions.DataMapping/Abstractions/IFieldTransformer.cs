namespace WorkflowFramework.Extensions.DataMapping.Abstractions;

/// <summary>
/// Transforms an individual field value during data mapping.
/// </summary>
public interface IFieldTransformer
{
    /// <summary>
    /// Gets the unique name of this transformer.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Transforms the input value.
    /// </summary>
    /// <param name="input">The input value to transform.</param>
    /// <param name="args">Optional arguments for the transformation.</param>
    /// <returns>The transformed value.</returns>
    string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null);
}
