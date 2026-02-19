namespace WorkflowFramework.Extensions.DataMapping.Abstractions;

/// <summary>
/// Maps data between formats using field mappings, source readers, destination writers, and transformers.
/// </summary>
public interface IDataMapper
{
    /// <summary>
    /// Applies the given mapping profile, reading from the source and writing to the destination.
    /// </summary>
    /// <typeparam name="TSource">The source data type.</typeparam>
    /// <typeparam name="TDestination">The destination data type.</typeparam>
    /// <param name="profile">The mapping profile to apply.</param>
    /// <param name="source">The source data.</param>
    /// <param name="destination">The destination data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mapping result.</returns>
    Task<DataMappingResult> MapAsync<TSource, TDestination>(
        DataMappingProfile profile,
        TSource source,
        TDestination destination,
        CancellationToken cancellationToken = default);
}
