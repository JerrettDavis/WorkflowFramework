namespace WorkflowFramework.Extensions.DataMapping.Abstractions;

/// <summary>
/// Loads mapping profiles from configuration, database, or files.
/// </summary>
public interface IMappingProfileProvider
{
    /// <summary>
    /// Gets a mapping profile by name.
    /// </summary>
    /// <param name="profileName">The profile name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The profile, or null if not found.</returns>
    Task<DataMappingProfile?> GetProfileAsync(string profileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available profile names.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The available profile names.</returns>
    Task<IReadOnlyList<string>> GetProfileNamesAsync(CancellationToken cancellationToken = default);
}
