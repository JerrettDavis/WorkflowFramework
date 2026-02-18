namespace WorkflowFramework.Extensions.DataMapping.Schema.Abstractions;

/// <summary>
/// Provides schemas for data formats.
/// </summary>
public interface ISchemaProvider
{
    /// <summary>
    /// Gets a schema by name.
    /// </summary>
    /// <param name="schemaName">The schema name.</param>
    /// <returns>The schema string, or null if not found.</returns>
    string? GetSchema(string schemaName);

    /// <summary>
    /// Gets all available schema names.
    /// </summary>
    IEnumerable<string> GetSchemaNames();
}

/// <summary>
/// Central registry for managing schemas with versioning support.
/// </summary>
public sealed class SchemaRegistry : ISchemaProvider
{
    private readonly Dictionary<string, SchemaEntry> _schemas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a schema.
    /// </summary>
    /// <param name="name">The schema name.</param>
    /// <param name="schema">The schema content.</param>
    /// <param name="version">Optional version.</param>
    public void Register(string name, string schema, int version = 1)
    {
        _schemas[name] = new SchemaEntry(schema, version);
    }

    /// <inheritdoc />
    public string? GetSchema(string schemaName) =>
        _schemas.TryGetValue(schemaName, out var entry) ? entry.Schema : null;

    /// <inheritdoc />
    public IEnumerable<string> GetSchemaNames() => _schemas.Keys;

    /// <summary>
    /// Gets the version of a schema.
    /// </summary>
    public int? GetSchemaVersion(string schemaName) =>
        _schemas.TryGetValue(schemaName, out var entry) ? entry.Version : null;

    private sealed record SchemaEntry(string Schema, int Version);
}
