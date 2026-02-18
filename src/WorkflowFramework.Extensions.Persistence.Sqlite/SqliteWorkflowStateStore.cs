using System.Text.Json;
using Microsoft.Data.Sqlite;
using WorkflowFramework.Persistence;

namespace WorkflowFramework.Extensions.Persistence.Sqlite;

/// <summary>
/// SQLite-based implementation of <see cref="IWorkflowStateStore"/>.
/// </summary>
public sealed class SqliteWorkflowStateStore : IWorkflowStateStore, IDisposable
{
    private readonly SqliteConnection _connection;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteWorkflowStateStore"/>.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public SqliteWorkflowStateStore(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        EnsureTable();
    }

    private void EnsureTable()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS WorkflowState (
                WorkflowId TEXT PRIMARY KEY,
                CorrelationId TEXT NOT NULL,
                WorkflowName TEXT NOT NULL,
                LastCompletedStepIndex INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                Properties TEXT,
                SerializedData TEXT,
                Timestamp TEXT NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public async Task SaveCheckpointAsync(string workflowId, WorkflowState state, CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO WorkflowState
            (WorkflowId, CorrelationId, WorkflowName, LastCompletedStepIndex, Status, Properties, SerializedData, Timestamp)
            VALUES ($wid, $cid, $name, $idx, $status, $props, $data, $ts)";
        cmd.Parameters.AddWithValue("$wid", state.WorkflowId);
        cmd.Parameters.AddWithValue("$cid", state.CorrelationId);
        cmd.Parameters.AddWithValue("$name", state.WorkflowName);
        cmd.Parameters.AddWithValue("$idx", state.LastCompletedStepIndex);
        cmd.Parameters.AddWithValue("$status", (int)state.Status);
        cmd.Parameters.AddWithValue("$props", JsonSerializer.Serialize(state.Properties, JsonOptions));
        cmd.Parameters.AddWithValue("$data", (object?)state.SerializedData ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ts", state.Timestamp.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkflowState?> LoadCheckpointAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM WorkflowState WHERE WorkflowId = $wid";
        cmd.Parameters.AddWithValue("$wid", workflowId);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new WorkflowState
        {
            WorkflowId = reader.GetString(0),
            CorrelationId = reader.GetString(1),
            WorkflowName = reader.GetString(2),
            LastCompletedStepIndex = reader.GetInt32(3),
            Status = (WorkflowStatus)reader.GetInt32(4),
            Properties = JsonSerializer.Deserialize<Dictionary<string, object?>>(reader.GetString(5)) ?? new(),
            SerializedData = reader.IsDBNull(6) ? null : reader.GetString(6),
            Timestamp = DateTimeOffset.Parse(reader.GetString(7))
        };
    }

    /// <inheritdoc />
    public async Task DeleteCheckpointAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM WorkflowState WHERE WorkflowId = $wid";
        cmd.Parameters.AddWithValue("$wid", workflowId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _connection.Dispose();
    }
}
