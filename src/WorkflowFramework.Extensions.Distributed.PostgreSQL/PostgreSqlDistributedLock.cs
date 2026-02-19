using Npgsql;

namespace WorkflowFramework.Extensions.Distributed.PostgreSQL;

/// <summary>
/// PostgreSQL-based implementation of <see cref="IDistributedLock"/> using advisory locks.
/// </summary>
public sealed class PostgreSqlDistributedLock : IDistributedLock
{
    private readonly Func<NpgsqlConnection> _connectionFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="PostgreSqlDistributedLock"/>.
    /// </summary>
    /// <param name="connectionFactory">Factory to create PostgreSQL connections.</param>
    public PostgreSqlDistributedLock(Func<NpgsqlConnection> connectionFactory)
    {
        if (connectionFactory is null) throw new ArgumentNullException(nameof(connectionFactory));
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        var lockId = GetLockId(key);
        var connection = _connectionFactory();
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT pg_try_advisory_lock(@lockId)";
            cmd.Parameters.AddWithValue("lockId", lockId);

            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (result is true)
            {
                return new AdvisoryLockHandle(connection, lockId);
            }

            await connection.DisposeAsync().ConfigureAwait(false);
            return null;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Converts a string key to a 64-bit lock identifier using a stable hash.
    /// </summary>
    /// <summary>
    /// Converts a string key to a 64-bit lock identifier using FNV-1a hash.
    /// </summary>
    public static long GetLockId(string key)
    {
        unchecked
        {
            const long fnvOffset = unchecked((long)0xcbf29ce484222325);
            const long fnvPrime = unchecked((long)0x100000001b3);
            var hash = fnvOffset;
            foreach (var c in key)
            {
                hash ^= c;
                hash *= fnvPrime;
            }
            return hash;
        }
    }

    private sealed class AdvisoryLockHandle : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly long _lockId;
        private int _disposed;

        public AdvisoryLockHandle(NpgsqlConnection connection, long lockId)
        {
            _connection = connection;
            _lockId = lockId;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                try
                {
                    using var cmd = _connection.CreateCommand();
                    cmd.CommandText = "SELECT pg_advisory_unlock(@lockId)";
                    cmd.Parameters.AddWithValue("lockId", _lockId);
                    await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                }
                finally
                {
                    await _connection.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
