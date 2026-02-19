using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Readers;

/// <summary>
/// Reads values from a <see cref="Dictionary{TKey,TValue}"/> using simple key lookup.
/// Supports dot-notation for nested dictionaries (e.g., <c>customer.name</c>).
/// </summary>
public sealed class DictionarySourceReader : ISourceReader<IDictionary<string, object?>>
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedPrefixes => [string.Empty];

    /// <inheritdoc />
    public bool CanRead(string path) =>
        !string.IsNullOrEmpty(path) && !path.StartsWith("$.", StringComparison.Ordinal) && !path.StartsWith("/", StringComparison.Ordinal);

    /// <inheritdoc />
    public string? Read(string path, IDictionary<string, object?> source)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var parts = path.Split('.');
        object? current = source;

        foreach (var part in parts)
        {
            if (current is IDictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(part, out current))
                    return null;
            }
            else
            {
                return null;
            }
        }

        return current?.ToString();
    }
}
