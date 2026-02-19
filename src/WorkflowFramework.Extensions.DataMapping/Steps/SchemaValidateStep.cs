namespace WorkflowFramework.Extensions.DataMapping.Steps;

/// <summary>
/// Workflow step that validates data against a schema.
/// Reads from <c>__Source</c> or <c>__Destination</c> depending on configuration.
/// </summary>
public sealed class SchemaValidateStep : StepBase
{
    private readonly ISchemaValidator _validator;
    private readonly string _schemaName;
    private readonly bool _validateDestination;

    /// <summary>
    /// Initializes a new instance of <see cref="SchemaValidateStep"/>.
    /// </summary>
    /// <param name="validator">The schema validator.</param>
    /// <param name="schemaName">The name of the schema to validate against.</param>
    /// <param name="validateDestination">If true, validates destination; otherwise validates source.</param>
    public SchemaValidateStep(ISchemaValidator validator, string schemaName, bool validateDestination = false)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _schemaName = schemaName;
        _validateDestination = validateDestination;
    }

    /// <inheritdoc />
    public override Task ExecuteAsync(IWorkflowContext context)
    {
        var key = _validateDestination ? DataMapStep.DestinationKey : DataMapStep.SourceKey;
        if (!context.Properties.TryGetValue(key, out var data) || data == null)
            throw new InvalidOperationException($"No data found in context property '{key}' for schema validation.");

        var dataStr = data as string ?? data.ToString()!;
        var result = _validator.Validate(dataStr, _schemaName);
        if (!result.IsValid)
            throw new InvalidOperationException(
                $"Schema validation failed for '{_schemaName}': {string.Join("; ", result.Errors)}");

        return Task.CompletedTask;
    }
}

/// <summary>
/// Validates data against a named schema.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Validates the data against the given schema.
    /// </summary>
    /// <param name="data">The data to validate.</param>
    /// <param name="schemaName">The schema name.</param>
    /// <returns>The validation result.</returns>
    SchemaValidationResult Validate(string data, string schemaName);
}

/// <summary>
/// Result of schema validation.
/// </summary>
public sealed class SchemaValidationResult
{
    /// <summary>
    /// Gets whether the data is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Creates a valid result.
    /// </summary>
    public static SchemaValidationResult Valid() => new() { IsValid = true };

    /// <summary>
    /// Creates an invalid result.
    /// </summary>
    public static SchemaValidationResult Invalid(IReadOnlyList<string> errors) =>
        new() { IsValid = false, Errors = errors };
}
