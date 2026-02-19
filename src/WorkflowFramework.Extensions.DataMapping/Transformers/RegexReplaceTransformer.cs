using System.Text.RegularExpressions;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using static WorkflowFramework.Extensions.DataMapping.Internal.DictHelper;

namespace WorkflowFramework.Extensions.DataMapping.Transformers;

/// <summary>
/// Applies regex replacement. Requires <c>pattern</c> and <c>replacement</c> args.
/// </summary>
public sealed class RegexReplaceTransformer : IFieldTransformer
{
    /// <inheritdoc />
    public string Name => "regexReplace";

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null)
    {
        if (string.IsNullOrEmpty(input) || args == null)
            return input;

        var pattern = TryGet(args, "pattern");
        var replacement = TryGet(args, "replacement") ?? string.Empty;

        if (string.IsNullOrEmpty(pattern))
            return input;

        return Regex.Replace(input, pattern, replacement);
    }
}
