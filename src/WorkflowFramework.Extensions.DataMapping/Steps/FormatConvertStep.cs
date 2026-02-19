using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Steps;

/// <summary>
/// Workflow step that converts data between formats (JSON↔XML↔CSV↔Dictionary).
/// Reads from <c>__Source</c> and writes converted data to <c>__Destination</c>.
/// Uses an <see cref="IFormatConverter"/> to perform the conversion.
/// </summary>
public sealed class FormatConvertStep : StepBase
{
    private readonly DataFormat _from;
    private readonly DataFormat _to;
    private readonly IFormatConverter _converter;

    /// <summary>
    /// Initializes a new instance of <see cref="FormatConvertStep"/>.
    /// </summary>
    /// <param name="converter">The format converter.</param>
    /// <param name="from">Source format.</param>
    /// <param name="to">Destination format.</param>
    public FormatConvertStep(IFormatConverter converter, DataFormat from, DataFormat to)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _from = from;
        _to = to;
    }

    /// <inheritdoc />
    public override Task ExecuteAsync(IWorkflowContext context)
    {
        if (!context.Properties.TryGetValue(DataMapStep.SourceKey, out var sourceObj) || sourceObj is not string sourceStr)
            throw new InvalidOperationException("FormatConvertStep requires a string source in context.");

        var converted = _converter.Convert(sourceStr, _from, _to);
        context.Properties[DataMapStep.DestinationKey] = converted;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Converts data between string representations of different formats.
/// </summary>
public interface IFormatConverter
{
    /// <summary>
    /// Converts a string from one format to another.
    /// </summary>
    /// <param name="input">The input string in the source format.</param>
    /// <param name="from">The source format.</param>
    /// <param name="to">The target format.</param>
    /// <returns>The converted string.</returns>
    string Convert(string input, DataFormat from, DataFormat to);

    /// <summary>
    /// Detects the format of the given content.
    /// </summary>
    /// <param name="content">The content to analyze.</param>
    /// <returns>The detected format.</returns>
    DataFormat DetectFormat(string content);
}
