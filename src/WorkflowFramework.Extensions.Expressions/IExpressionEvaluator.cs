namespace WorkflowFramework.Extensions.Expressions;

/// <summary>
/// Abstraction for evaluating dynamic expressions.
/// </summary>
public interface IExpressionEvaluator
{
    /// <summary>Gets the name of this evaluator (e.g. "simple", "csharp").</summary>
    string Name { get; }

    /// <summary>
    /// Evaluates an expression and returns the result.
    /// </summary>
    /// <typeparam name="T">The expected result type.</typeparam>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="variables">Variables available to the expression.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the evaluation.</returns>
    Task<T?> EvaluateAsync<T>(string expression, IDictionary<string, object?> variables, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates an expression and returns the result as object.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="variables">Variables available to the expression.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the evaluation.</returns>
    Task<object?> EvaluateAsync(string expression, IDictionary<string, object?> variables, CancellationToken cancellationToken = default);
}
