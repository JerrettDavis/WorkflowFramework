using System.Text.Json;
using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Readers;

/// <summary>
/// Reads values from a <see cref="JsonDocument"/> or <see cref="JsonElement"/> using simple dot-notation JSONPath.
/// Supports paths like <c>$.orders[0].id</c>, <c>$.customer.name</c>.
/// </summary>
public sealed class JsonSourceReader : ISourceReader<JsonElement>
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedPrefixes => ["$."];

    /// <inheritdoc />
    public bool CanRead(string path) => path.StartsWith("$.", StringComparison.Ordinal);

    /// <inheritdoc />
    public string? Read(string path, JsonElement source)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("$.", StringComparison.Ordinal))
            return null;

        try
        {
            var segments = ParsePath(path[2..]);
            var current = source;

            foreach (var segment in segments)
            {
                if (segment.ArrayIndex.HasValue)
                {
                    if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment.Name, out var arr))
                        return null;
                    if (arr.ValueKind != JsonValueKind.Array || segment.ArrayIndex.Value >= arr.GetArrayLength())
                        return null;
                    current = arr[segment.ArrayIndex.Value];
                }
                else
                {
                    if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment.Name, out var next))
                        return null;
                    current = next;
                }
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString(),
                JsonValueKind.Number => current.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => current.GetRawText()
            };
        }
        catch
        {
            return null;
        }
    }

    private static List<PathSegment> ParsePath(string path)
    {
        var segments = new List<PathSegment>();
        foreach (var part in path.Split('.'))
        {
            var bracketIdx = part.IndexOf('[');
            if (bracketIdx >= 0)
            {
                var name = part[..bracketIdx];
                var indexStr = part[(bracketIdx + 1)..part.IndexOf(']')];
                segments.Add(new PathSegment(name, int.Parse(indexStr)));
            }
            else
            {
                segments.Add(new PathSegment(part, null));
            }
        }
        return segments;
    }

    private readonly record struct PathSegment(string Name, int? ArrayIndex);
}
