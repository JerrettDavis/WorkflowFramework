using System.Text.RegularExpressions;

namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// Renders simple workflow property placeholders in prompt templates.
/// </summary>
public static class PromptTemplateRenderer
{
    private static readonly Regex DirectTokenPattern = new(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex LegacyTokenPattern = new(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Replaces legacy <c>{PropertyName}</c> tokens and richer <c>{{Step Name.Response}}</c> tokens
    /// with matching workflow property values.
    /// Missing or null values leave the original placeholder unchanged.
    /// </summary>
    public static string Render(string? template, IDictionary<string, object?> properties)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));

        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        var rendered = DirectTokenPattern.Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            var value = ResolvePropertyValue(properties, key);
            return value ?? match.Value;
        });

        return LegacyTokenPattern.Replace(rendered, match =>
        {
            var key = match.Groups[1].Value;
            var value = ResolvePropertyValue(properties, key);
            return value ?? match.Value;
        });
    }

    private static string? ResolvePropertyValue(IDictionary<string, object?> properties, string key)
    {
        if (properties.TryGetValue(key, out var direct) && direct is not null)
            return Convert.ToString(direct);

        if (!key.Contains('.'))
            return null;

        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return null;

        if (!properties.TryGetValue(segments[0], out var current))
            return null;

        for (var i = 1; i < segments.Length; i++)
        {
            if (current is not IDictionary<string, object?> objectMap)
                return null;

            if (!objectMap.TryGetValue(segments[i], out current))
                return null;
        }

        return current is null ? null : Convert.ToString(current);
    }
}
