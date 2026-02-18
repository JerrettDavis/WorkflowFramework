using System.Reflection;
using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Readers;

/// <summary>
/// Reads values from CLR objects via reflection using dot-notation property paths.
/// Paths use <c>@.</c> prefix (e.g., <c>@.Customer.Name</c>).
/// </summary>
public sealed class ObjectSourceReader : ISourceReader<object>
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedPrefixes => ["@."];

    /// <inheritdoc />
    public bool CanRead(string path) => path.StartsWith("@.", StringComparison.Ordinal);

    /// <inheritdoc />
    public string? Read(string path, object source)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("@.", StringComparison.Ordinal))
            return null;

        try
        {
            var propertyPath = path[2..];
            var parts = propertyPath.Split('.');
            object? current = source;

            foreach (var part in parts)
            {
                if (current == null)
                    return null;

                var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null)
                    return null;

                current = prop.GetValue(current);
            }

            return current?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
