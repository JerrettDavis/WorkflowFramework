using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Engine;

/// <summary>
/// Core data mapping engine that coordinates source readers, destination writers, and transformers.
/// </summary>
public sealed class DataMapper : IDataMapper
{
    private readonly IFieldTransformerRegistry _transformerRegistry;
    private readonly IEnumerable<object> _readers;
    private readonly IEnumerable<object> _writers;

    /// <summary>
    /// Initializes a new instance of <see cref="DataMapper"/>.
    /// </summary>
    /// <param name="transformerRegistry">The transformer registry.</param>
    /// <param name="readers">All registered source readers.</param>
    /// <param name="writers">All registered destination writers.</param>
    public DataMapper(
        IFieldTransformerRegistry transformerRegistry,
        IEnumerable<object> readers,
        IEnumerable<object> writers)
    {
        _transformerRegistry = transformerRegistry ?? throw new ArgumentNullException(nameof(transformerRegistry));
        _readers = readers ?? throw new ArgumentNullException(nameof(readers));
        _writers = writers ?? throw new ArgumentNullException(nameof(writers));
    }

    /// <inheritdoc />
    public Task<DataMappingResult> MapAsync<TSource, TDestination>(
        DataMappingProfile profile,
        TSource source,
        TDestination destination,
        CancellationToken cancellationToken = default)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destination == null) throw new ArgumentNullException(nameof(destination));

        var typedReaders = _readers.OfType<ISourceReader<TSource>>().ToList();
        var typedWriters = _writers.OfType<IDestinationWriter<TDestination>>().ToList();

        var errors = new List<string>();
        var mapped = 0;
        var total = profile.Mappings.Count;

        foreach (var field in profile.Mappings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Read source value
                string? value = null;
                foreach (var reader in typedReaders)
                {
                    if (reader.CanRead(field.SourcePath))
                    {
                        value = reader.Read(field.SourcePath, source);
                        break;
                    }
                }

                // Apply default if null
                if (value == null && profile.Defaults.TryGetValue(field.DestinationPath, out var defaultValue))
                    value = defaultValue;

                // Apply transformers
                value = _transformerRegistry.ApplyAll(value, field.Transformers);

                // Write to destination
                var written = false;
                foreach (var writer in typedWriters)
                {
                    if (writer.CanWrite(field.DestinationPath))
                    {
                        written = writer.Write(field.DestinationPath, value, destination);
                        break;
                    }
                }

                if (written)
                    mapped++;
                else
                    errors.Add($"No writer found for destination path: {field.DestinationPath}");
            }
            catch (Exception ex)
            {
                errors.Add($"Error mapping {field.SourcePath} -> {field.DestinationPath}: {ex.Message}");
            }
        }

        var result = errors.Count == 0
            ? DataMappingResult.Success(mapped, total)
            : DataMappingResult.Failure(errors, mapped, total);

        return Task.FromResult(result);
    }
}
