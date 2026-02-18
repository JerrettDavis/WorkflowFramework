using System.Text.RegularExpressions;

namespace WorkflowFramework.Extensions.Expressions;

/// <summary>
/// Provides template string interpolation using <c>{{variable}}</c> syntax.
/// </summary>
public sealed class TemplateEngine
{
    private readonly IExpressionEvaluator _evaluator;

    /// <summary>
    /// Initializes a new instance of <see cref="TemplateEngine"/>.
    /// </summary>
    /// <param name="evaluator">The expression evaluator to use for resolving template expressions.</param>
    public TemplateEngine(IExpressionEvaluator? evaluator = null)
    {
        _evaluator = evaluator ?? new SimpleExpressionEvaluator();
    }

    /// <summary>
    /// Renders a template by replacing <c>{{expression}}</c> placeholders with evaluated values.
    /// </summary>
    /// <param name="template">The template string.</param>
    /// <param name="variables">Variables available for expression evaluation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rendered string.</returns>
    public async Task<string> RenderAsync(string template, IDictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var result = template;
        var matches = Regex.Matches(template, @"\{\{(.+?)\}\}");
        foreach (Match match in matches)
        {
            var expr = match.Groups[1].Value.Trim();
            var value = await _evaluator.EvaluateAsync(expr, variables, cancellationToken).ConfigureAwait(false);
            result = result.Replace(match.Value, value?.ToString() ?? string.Empty);
        }
        return result;
    }
}
