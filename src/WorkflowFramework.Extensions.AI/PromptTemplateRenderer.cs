using System.Text.RegularExpressions;

namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// Renders simple workflow property placeholders in prompt templates.
/// </summary>
public static class PromptTemplateRenderer
{
    /// <summary>
    /// Replaces <c>{PropertyName}</c> tokens with matching workflow property values.
    /// Missing or null values leave the original placeholder unchanged.
    /// </summary>
    public static string Render(string? template, IDictionary<string, object?> properties)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));

        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        return Regex.Replace(template, @"\{(\w+)\}", match =>
        {
            var key = match.Groups[1].Value;
            if (properties.TryGetValue(key, out var value) && value is not null)
            {
                return value.ToString() ?? string.Empty;
            }

            return match.Value;
        });
    }
}
