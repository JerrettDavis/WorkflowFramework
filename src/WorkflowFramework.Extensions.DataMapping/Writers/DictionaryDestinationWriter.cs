using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Writers;

/// <summary>
/// Writes values to a <see cref="Dictionary{TKey,TValue}"/> using dot-notation paths.
/// Creates nested dictionaries as needed.
/// </summary>
public sealed class DictionaryDestinationWriter : IDestinationWriter<IDictionary<string, object?>>
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedPrefixes => [string.Empty];

    /// <inheritdoc />
    public bool CanWrite(string path) =>
        !string.IsNullOrEmpty(path) && !path.StartsWith("$.", StringComparison.Ordinal) && !path.StartsWith("/", StringComparison.Ordinal);

    /// <inheritdoc />
    public bool Write(string path, string? value, IDictionary<string, object?> destination)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        try
        {
            var parts = path.Split('.');
            var current = destination;

            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (!current.TryGetValue(parts[i], out var existing) || existing is not IDictionary<string, object?> nested)
                {
                    nested = new Dictionary<string, object?>();
                    current[parts[i]] = nested;
                }
                current = nested;
            }

            current[parts[^1]] = value;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
