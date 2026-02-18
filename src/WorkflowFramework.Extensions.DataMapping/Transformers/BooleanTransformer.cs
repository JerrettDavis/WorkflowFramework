using WorkflowFramework.Extensions.DataMapping.Abstractions;
using static WorkflowFramework.Extensions.DataMapping.Internal.DictHelper;

namespace WorkflowFramework.Extensions.DataMapping.Transformers;

/// <summary>
/// Converts values to/from various boolean representations (Y/N, true/false, 1/0).
/// Supports <c>trueValue</c> and <c>falseValue</c> args for custom output.
/// </summary>
public sealed class BooleanTransformer : IFieldTransformer
{
    private static readonly HashSet<string> TrueValues = new(StringComparer.OrdinalIgnoreCase)
        { "true", "t", "yes", "y", "1", "on" };

    /// <inheritdoc />
    public string Name => "toBoolean";

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null)
    {
        var trueOutput = TryGet(args, "trueValue") ?? "True";
        var falseOutput = TryGet(args, "falseValue") ?? "False";

        if (string.IsNullOrWhiteSpace(input))
            return falseOutput;

        return TrueValues.Contains(input.Trim()) ? trueOutput : falseOutput;
    }
}
