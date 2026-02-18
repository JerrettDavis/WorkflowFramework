using WorkflowFramework.Builder;

namespace WorkflowFramework.Extensions.Expressions;

/// <summary>
/// Builder extensions for expression-based workflow steps.
/// </summary>
public static class ExpressionBuilderExtensions
{
    /// <summary>
    /// Adds a conditional step that evaluates a dynamic expression.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="expression">The expression to evaluate (must return boolean).</param>
    /// <param name="evaluator">The expression evaluator to use.</param>
    /// <returns>A conditional builder.</returns>
    public static IConditionalBuilder IfExpression(this IWorkflowBuilder builder, string expression, IExpressionEvaluator? evaluator = null)
    {
        var eval = evaluator ?? new SimpleExpressionEvaluator();
        return builder.If(ctx =>
        {
            var result = eval.EvaluateAsync<bool>(expression, ctx.Properties).GetAwaiter().GetResult();
            return result;
        });
    }
}
