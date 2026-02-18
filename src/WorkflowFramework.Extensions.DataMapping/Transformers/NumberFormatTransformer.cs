using System.Globalization;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using static WorkflowFramework.Extensions.DataMapping.Internal.DictHelper;

namespace WorkflowFramework.Extensions.DataMapping.Transformers;

/// <summary>
/// Formats numbers. Supports <c>format</c> arg (e.g., "C2" for currency, "N0" for integer).
/// Optionally accepts <c>culture</c> arg.
/// </summary>
public sealed class NumberFormatTransformer : IFieldTransformer
{
    /// <inheritdoc />
    public string Name => "numberFormat";

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        if (!decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
            return input;

        var format = TryGet(args, "format") ?? "N2";
        var cultureName = TryGet(args, "culture");
        var culture = string.IsNullOrEmpty(cultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(cultureName);

        return number.ToString(format, culture);
    }
}
