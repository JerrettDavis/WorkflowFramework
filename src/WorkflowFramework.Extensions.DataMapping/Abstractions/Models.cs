namespace WorkflowFramework.Extensions.DataMapping.Abstractions;

/// <summary>
/// A reference to a transformer with optional arguments.
/// </summary>
/// <param name="Name">The transformer name.</param>
/// <param name="Args">Optional arguments for the transformer.</param>
public sealed record TransformerRef(
    string Name,
    IReadOnlyDictionary<string, string?>? Args = null);

/// <summary>
/// Defines a single field mapping from source path to destination path with an optional transformer chain.
/// </summary>
/// <param name="SourcePath">The path expression to read from the source.</param>
/// <param name="DestinationPath">The path expression to write to the destination.</param>
/// <param name="Transformers">Optional chain of transformers to apply to the value.</param>
public sealed record FieldMapping(
    string SourcePath,
    string DestinationPath,
    IReadOnlyList<TransformerRef>? Transformers = null);

/// <summary>
/// A named collection of field mappings that defines how to map data between formats.
/// </summary>
public sealed class DataMappingProfile
{
    /// <summary>
    /// Gets or sets the unique name of this profile.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the field mappings in this profile.
    /// </summary>
    public IList<FieldMapping> Mappings { get; } = new List<FieldMapping>();

    /// <summary>
    /// Gets the default values to apply when a source value is null.
    /// Key is the destination path, value is the default.
    /// </summary>
    public IDictionary<string, string> Defaults { get; } = new Dictionary<string, string>();
}

/// <summary>
/// Result of a data mapping operation.
/// </summary>
public sealed class DataMappingResult
{
    /// <summary>
    /// Gets whether the mapping was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the number of fields successfully mapped.
    /// </summary>
    public int MappedFieldCount { get; init; }

    /// <summary>
    /// Gets the total number of field mappings attempted.
    /// </summary>
    public int TotalFieldCount { get; init; }

    /// <summary>
    /// Gets any errors that occurred during mapping.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static DataMappingResult Success(int mapped, int total) =>
        new() { IsSuccess = true, MappedFieldCount = mapped, TotalFieldCount = total };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static DataMappingResult Failure(IReadOnlyList<string> errors, int mapped = 0, int total = 0) =>
        new() { IsSuccess = false, Errors = errors, MappedFieldCount = mapped, TotalFieldCount = total };
}

/// <summary>
/// Supported data formats.
/// </summary>
public enum DataFormat
{
    /// <summary>JSON format.</summary>
    Json,
    /// <summary>XML format.</summary>
    Xml,
    /// <summary>CSV format.</summary>
    Csv,
    /// <summary>YAML format.</summary>
    Yaml,
    /// <summary>Dictionary format.</summary>
    Dictionary
}
