using System.Text.Json.Nodes;
using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Writers;

/// <summary>
/// Writes values to a <see cref="JsonObject"/> using dot-notation paths prefixed with <c>$.</c>.
/// Creates intermediate objects as needed.
/// </summary>
public sealed class JsonDestinationWriter : IDestinationWriter<JsonObject>
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedPrefixes => ["$."];

    /// <inheritdoc />
    public bool CanWrite(string path) => path.StartsWith("$.", StringComparison.Ordinal);

    /// <inheritdoc />
    public bool Write(string path, string? value, JsonObject destination)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("$.", StringComparison.Ordinal))
            return false;

        try
        {
            var segments = path[2..].Split('.');
            var current = destination;

            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (current[segments[i]] is not JsonObject nested)
                {
                    nested = new JsonObject();
                    current[segments[i]] = nested;
                }
                current = nested;
            }

            current[segments[^1]] = value == null ? null : JsonValue.Create(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
