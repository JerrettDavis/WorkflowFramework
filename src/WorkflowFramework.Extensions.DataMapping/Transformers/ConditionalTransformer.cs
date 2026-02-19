using WorkflowFramework.Extensions.DataMapping.Abstractions;
using static WorkflowFramework.Extensions.DataMapping.Internal.DictHelper;

namespace WorkflowFramework.Extensions.DataMapping.Transformers;

/// <summary>
/// Returns different values based on the input matching a condition.
/// Args: <c>equals</c> — value to compare against, <c>then</c> — value if matched, <c>else</c> — value if not matched.
/// </summary>
public sealed class ConditionalTransformer : IFieldTransformer
{
    /// <inheritdoc />
    public string Name => "conditional";

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null)
    {
        if (args == null)
            return input;

        var equalsValue = TryGet(args, "equals");
        var thenValue = TryGet(args, "then");
        var elseValue = TryGet(args, "else");

        if (string.Equals(input, equalsValue, StringComparison.OrdinalIgnoreCase))
            return thenValue ?? input;

        return elseValue ?? input;
    }
}
