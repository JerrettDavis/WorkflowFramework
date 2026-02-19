namespace WorkflowFramework.Extensions.DataMapping.Abstractions;

/// <summary>
/// Writes values to a destination data object by path expression.
/// </summary>
/// <typeparam name="TDestination">The type of destination data.</typeparam>
public interface IDestinationWriter<in TDestination>
{
    /// <summary>
    /// Gets the path prefixes this writer supports.
    /// </summary>
    IReadOnlyList<string> SupportedPrefixes { get; }

    /// <summary>
    /// Determines whether this writer can handle the given path.
    /// </summary>
    /// <param name="path">The destination path expression.</param>
    /// <returns>True if this writer can write to the path.</returns>
    bool CanWrite(string path);

    /// <summary>
    /// Writes a value to the destination at the specified path.
    /// </summary>
    /// <param name="path">The path expression.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="destination">The destination data object.</param>
    /// <returns>True if the write was successful.</returns>
    bool Write(string path, string? value, TDestination destination);
}
