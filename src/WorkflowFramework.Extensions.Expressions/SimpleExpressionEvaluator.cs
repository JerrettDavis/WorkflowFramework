using System.Globalization;
using System.Text.RegularExpressions;

namespace WorkflowFramework.Extensions.Expressions;

/// <summary>
/// A simple expression evaluator that supports basic operations:
/// variable references, comparisons, boolean logic, arithmetic, and string interpolation.
/// </summary>
public sealed class SimpleExpressionEvaluator : IExpressionEvaluator
{
    /// <inheritdoc />
    public string Name => "simple";

    /// <inheritdoc />
    public Task<T?> EvaluateAsync<T>(string expression, IDictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        var result = Evaluate(expression, variables);
        if (result == null) return Task.FromResult(default(T));
        return Task.FromResult((T?)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public Task<object?> EvaluateAsync(string expression, IDictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Evaluate(expression, variables));
    }

    private static object? Evaluate(string expression, IDictionary<string, object?> variables)
    {
        var expr = expression.Trim();

        // Boolean literals
        if (expr.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (expr.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        if (expr.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;

        // Numeric literal
        if (double.TryParse(expr, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            return num;

        // String literal
        if ((expr.StartsWith("'") && expr.EndsWith("'")) || (expr.StartsWith("\"") && expr.EndsWith("\"")))
            return expr.Substring(1, expr.Length - 2);

        // Comparison operators
        foreach (var op in new[] { "==", "!=", ">=", "<=", ">", "<" })
        {
            var idx = expr.IndexOf(op, StringComparison.Ordinal);
            if (idx > 0)
            {
                var left = Evaluate(expr.Substring(0, idx), variables);
                var right = Evaluate(expr.Substring(idx + op.Length), variables);
                return EvaluateComparison(left, right, op);
            }
        }

        // Boolean operators
        var andIdx = expr.IndexOf("&&", StringComparison.Ordinal);
        if (andIdx > 0)
        {
            var left = Convert.ToBoolean(Evaluate(expr.Substring(0, andIdx), variables), CultureInfo.InvariantCulture);
            var right = Convert.ToBoolean(Evaluate(expr.Substring(andIdx + 2), variables), CultureInfo.InvariantCulture);
            return left && right;
        }

        var orIdx = expr.IndexOf("||", StringComparison.Ordinal);
        if (orIdx > 0)
        {
            var left = Convert.ToBoolean(Evaluate(expr.Substring(0, orIdx), variables), CultureInfo.InvariantCulture);
            var right = Convert.ToBoolean(Evaluate(expr.Substring(orIdx + 2), variables), CultureInfo.InvariantCulture);
            return left || right;
        }

        // Arithmetic
        foreach (var op in new[] { '+', '-', '*', '/' })
        {
            var idx = expr.LastIndexOf(op);
            if (idx > 0)
            {
                var left = Convert.ToDouble(Evaluate(expr.Substring(0, idx), variables), CultureInfo.InvariantCulture);
                var right = Convert.ToDouble(Evaluate(expr.Substring(idx + 1), variables), CultureInfo.InvariantCulture);
                return op switch
                {
                    '+' => left + right,
                    '-' => left - right,
                    '*' => left * right,
                    '/' => right != 0 ? left / right : throw new DivideByZeroException(),
                    _ => throw new InvalidOperationException($"Unknown operator: {op}")
                };
            }
        }

        // Variable lookup
        if (variables.TryGetValue(expr, out var val))
            return val;

        throw new InvalidOperationException($"Cannot evaluate expression: '{expression}'");
    }

    private static bool EvaluateComparison(object? left, object? right, string op)
    {
        if (left == null && right == null) return op == "==" || op == ">=" || op == "<=";
        if (left == null || right == null) return op == "!=";

        var l = Convert.ToDouble(left, CultureInfo.InvariantCulture);
        var r = Convert.ToDouble(right, CultureInfo.InvariantCulture);

        return op switch
        {
            "==" => Math.Abs(l - r) < 0.0001,
            "!=" => Math.Abs(l - r) >= 0.0001,
            ">" => l > r,
            "<" => l < r,
            ">=" => l >= r,
            "<=" => l <= r,
            _ => false
        };
    }
}
