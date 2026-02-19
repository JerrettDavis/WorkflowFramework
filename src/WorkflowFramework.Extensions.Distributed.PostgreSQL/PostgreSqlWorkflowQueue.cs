using System.Text.Json;
using Npgsql;

namespace WorkflowFramework.Extensions.Distributed.PostgreSQL;

/// <summary>
/// PostgreSQL-based implementation of <see cref="IWorkflowQueue"/> using a table and pg_notify.
/// </summary>
public sealed class PostgreSqlWorkflowQueue : IWorkflowQueue, IDisposable
{
    private readonly Func<NpgsqlConnection> _connectionFactory;
    private readonly string _tableName;
    private readonly string _channelName;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Initializes a new instance of <see cref="PostgreSqlWorkflowQueue"/>.
    /// </summary>
    /// <param name="connectionFactory">Factory to create PostgreSQL connections.</param>
    /// <param name="tableName">The table name for the queue.</param>
    /// <param name="channelName">The notification channel name.</param>
    public PostgreSqlWorkflowQueue(
        Func<NpgsqlConnection> connectionFactory,
        string tableName = "workflow_queue",
        string channelName = "workflow_queue_notify")
    {
        if (connectionFactory is null) throw new ArgumentNullException(nameof(connectionFactory));
        _connectionFactory = connectionFactory;
        _tableName = tableName;
        _channelName = channelName;
    }

    /// <summary>
    /// Ensures the queue table exists. Call once at startup.
    /// </summary>
    public async Task EnsureTableAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {_tableName} (
                seq_id BIGSERIAL PRIMARY KEY,
                id TEXT NOT NULL,
                workflow_name TEXT NOT NULL,
                serialized_data TEXT,
                enqueued_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            )";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(WorkflowQueueItem item, CancellationToken cancellationToken = default)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));

        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {_tableName} (id, workflow_name, serialized_data, enqueued_at)
            VALUES (@id, @workflowName, @serializedData, @enqueuedAt);
            SELECT pg_notify(@channel, @id)";
        cmd.Parameters.AddWithValue("id", item.Id);
        cmd.Parameters.AddWithValue("workflowName", item.WorkflowName);
        cmd.Parameters.AddWithValue("serializedData", (object?)item.SerializedData ?? DBNull.Value);
        cmd.Parameters.AddWithValue("enqueuedAt", item.EnqueuedAt);
        cmd.Parameters.AddWithValue("channel", _channelName);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkflowQueueItem?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            DELETE FROM {_tableName}
            WHERE seq_id = (
                SELECT seq_id FROM {_tableName}
                ORDER BY seq_id ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, workflow_name, serialized_data, enqueued_at";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new WorkflowQueueItem
            {
                Id = reader.GetString(0),
                WorkflowName = reader.GetString(1),
                SerializedData = reader.IsDBNull(2) ? null : reader.GetString(2),
                EnqueuedAt = reader.GetFieldValue<DateTimeOffset>(3)
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<int> GetLengthAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*)::int FROM {_tableName}";

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (int)(result ?? 0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No persistent resources to clean up — connections are created per-operation.
    }
}
