using System.Text.RegularExpressions;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Invokes a single named tool from a <see cref="ToolRegistry"/>.
/// Supports {PropertyName} substitution from context properties.
/// </summary>
public sealed class ToolCallStep : IStep
{
    private readonly ToolRegistry _registry;
    private readonly string _toolName;
    private readonly string _argumentsTemplate;
    private readonly string? _stepName;

    /// <summary>
    /// Initializes a new instance of <see cref="ToolCallStep"/>.
    /// </summary>
    public ToolCallStep(ToolRegistry registry, string toolName, string argumentsTemplate, string? stepName = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _toolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
        _argumentsTemplate = argumentsTemplate ?? throw new ArgumentNullException(nameof(argumentsTemplate));
        _stepName = stepName;
    }

    /// <inheritdoc />
    public string Name => _stepName ?? $"ToolCall.{_toolName}";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var arguments = SubstituteProperties(_argumentsTemplate, context.Properties);
        var result = await _registry.InvokeAsync(_toolName, arguments, context.CancellationToken).ConfigureAwait(false);
        context.Properties[$"{Name}.Result"] = result.Content;
        context.Properties[$"{Name}.IsError"] = result.IsError;
    }

    /// <summary>Substitutes {PropertyName} placeholders from context properties.</summary>
    public static string SubstituteProperties(string template, IDictionary<string, object?> properties)
    {
        return Regex.Replace(template, @"\{(\w+)\}", match =>
        {
            var key = match.Groups[1].Value;
            if (properties.TryGetValue(key, out var value) && value != null)
            {
                return value.ToString() ?? string.Empty;
            }
            return match.Value;
        });
    }
}
