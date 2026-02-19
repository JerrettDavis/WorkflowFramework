namespace WorkflowFramework.Extensions.DataMapping.Abstractions;

/// <summary>
/// Reads values from a source data object by path expression.
/// </summary>
/// <typeparam name="TSource">The type of source data.</typeparam>
public interface ISourceReader<in TSource>
{
    /// <summary>
    /// Gets the path prefixes this reader supports (e.g., "$." for JSON).
    /// </summary>
    IReadOnlyList<string> SupportedPrefixes { get; }

    /// <summary>
    /// Determines whether this reader can handle the given path.
    /// </summary>
    /// <param name="path">The source path expression.</param>
    /// <returns>True if this reader can resolve the path.</returns>
    bool CanRead(string path);

    /// <summary>
    /// Reads a value from the source at the specified path.
    /// </summary>
    /// <param name="path">The path expression.</param>
    /// <param name="source">The source data.</param>
    /// <returns>The resolved value as a string, or null if not found.</returns>
    string? Read(string path, TSource source);
}
