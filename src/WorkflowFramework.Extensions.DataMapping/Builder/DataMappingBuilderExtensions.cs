using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Batch;
using WorkflowFramework.Extensions.DataMapping.Steps;

namespace WorkflowFramework.Extensions.DataMapping.Builder;

/// <summary>
/// Fluent builder extensions for data mapping steps.
/// </summary>
public static class DataMappingBuilderExtensions
{
    /// <summary>
    /// Adds a data mapping step using an inline profile configuration.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="mapper">The data mapper.</param>
    /// <param name="configure">A delegate to configure the mapping profile.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder MapData(
        this IWorkflowBuilder builder,
        IDataMapper mapper,
        Action<DataMappingProfileBuilder> configure)
    {
        var profileBuilder = new DataMappingProfileBuilder();
        configure(profileBuilder);
        var profile = profileBuilder.Build();
        return builder.Step(new DataMapStep(mapper, profile));
    }

    /// <summary>
    /// Adds a data mapping step using a pre-built profile.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="mapper">The data mapper.</param>
    /// <param name="profile">The mapping profile.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder MapData(
        this IWorkflowBuilder builder,
        IDataMapper mapper,
        DataMappingProfile profile)
    {
        return builder.Step(new DataMapStep(mapper, profile));
    }

    /// <summary>
    /// Adds a format conversion step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="converter">The format converter.</param>
    /// <param name="from">Source format.</param>
    /// <param name="to">Destination format.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder ConvertFormat(
        this IWorkflowBuilder builder,
        IFormatConverter converter,
        DataFormat from,
        DataFormat to)
    {
        return builder.Step(new FormatConvertStep(converter, from, to));
    }

    /// <summary>
    /// Adds a schema validation step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="validator">The schema validator.</param>
    /// <param name="schemaName">The schema name to validate against.</param>
    /// <param name="validateDestination">If true, validates destination data; otherwise validates source.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder ValidateSchema(
        this IWorkflowBuilder builder,
        ISchemaValidator validator,
        string schemaName,
        bool validateDestination = false)
    {
        return builder.Step(new SchemaValidateStep(validator, schemaName, validateDestination));
    }

    /// <summary>
    /// Adds a batch processing step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="processBatch">The delegate to process each batch.</param>
    /// <param name="configure">Optional batch options configuration.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder BatchProcess(
        this IWorkflowBuilder builder,
        Func<IReadOnlyList<object>, IWorkflowContext, Task> processBatch,
        Action<BatchOptions>? configure = null)
    {
        var options = new BatchOptions();
        configure?.Invoke(options);
        return builder.Step(new BatchProcessStep(processBatch, options));
    }
}

/// <summary>
/// Fluent builder for constructing a <see cref="DataMappingProfile"/>.
/// </summary>
public sealed class DataMappingProfileBuilder
{
    private readonly DataMappingProfile _profile = new();

    /// <summary>
    /// Sets the profile name.
    /// </summary>
    public DataMappingProfileBuilder Named(string name)
    {
        _profile.Name = name;
        return this;
    }

    /// <summary>
    /// Adds a field mapping.
    /// </summary>
    /// <param name="sourcePath">The source path.</param>
    /// <param name="destinationPath">The destination path.</param>
    /// <param name="transformers">Optional transformer chain.</param>
    /// <returns>This builder for chaining.</returns>
    public DataMappingProfileBuilder Map(string sourcePath, string destinationPath, params TransformerRef[] transformers)
    {
        _profile.Mappings.Add(new FieldMapping(sourcePath, destinationPath,
            transformers.Length > 0 ? transformers : null));
        return this;
    }

    /// <summary>
    /// Sets a default value for a destination path when the source is null.
    /// </summary>
    /// <param name="destinationPath">The destination path.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>This builder for chaining.</returns>
    public DataMappingProfileBuilder WithDefault(string destinationPath, string defaultValue)
    {
        _profile.Defaults[destinationPath] = defaultValue;
        return this;
    }

    /// <summary>
    /// Builds the mapping profile.
    /// </summary>
    public DataMappingProfile Build() => _profile;
}
