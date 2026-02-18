using WorkflowFramework.Extensions.DataMapping.Abstractions;
using static WorkflowFramework.Extensions.DataMapping.Internal.DictHelper;

namespace WorkflowFramework.Extensions.DataMapping.Transformers;

/// <summary>
/// Provides a default value when the input is null or empty.
/// Requires <c>default</c> arg.
/// </summary>
public sealed class DefaultValueTransformer : IFieldTransformer
{
    /// <inheritdoc />
    public string Name => "default";

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null)
    {
        if (!string.IsNullOrEmpty(input))
            return input;

        return TryGet(args, "default");
    }
}
