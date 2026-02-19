using System.Globalization;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using static WorkflowFramework.Extensions.DataMapping.Internal.DictHelper;

namespace WorkflowFramework.Extensions.DataMapping.Transformers;

/// <summary>
/// Parses and reformats date values. Supports <c>inputFormat</c> and <c>outputFormat</c> args.
/// </summary>
public sealed class DateFormatTransformer : IFieldTransformer
{
    /// <inheritdoc />
    public string Name => "dateFormat";

    /// <inheritdoc />
    public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var inputFormat = TryGet(args, "inputFormat");
        var outputFormat = TryGet(args, "outputFormat") ?? "yyyy-MM-dd";

        DateTimeOffset dt;
        if (!string.IsNullOrEmpty(inputFormat))
        {
            if (!DateTimeOffset.TryParseExact(input, inputFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return input;
        }
        else
        {
            if (!DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return input;
        }

        return dt.ToString(outputFormat, CultureInfo.InvariantCulture);
    }
}
