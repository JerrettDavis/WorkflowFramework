using StackExchange.Redis;

namespace WorkflowFramework.Extensions.Distributed.Redis;

/// <summary>
/// Redis-based implementation of <see cref="IDistributedLock"/>.
/// </summary>
public sealed class RedisDistributedLock : IDistributedLock
{
    private readonly IDatabase _database;

    /// <summary>
    /// Initializes a new instance of <see cref="RedisDistributedLock"/>.
    /// </summary>
    /// <param name="database">The Redis database.</param>
    public RedisDistributedLock(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var lockKey = $"workflow:lock:{key}";
        var lockValue = Guid.NewGuid().ToString("N");

        var acquired = await _database.StringSetAsync(lockKey, lockValue, expiry, When.NotExists)
            .ConfigureAwait(false);

        return acquired ? new RedisLockHandle(_database, lockKey, lockValue) : null;
    }

    private sealed class RedisLockHandle : IAsyncDisposable
    {
        private readonly IDatabase _database;
        private readonly string _key;
        private readonly string _value;
        private int _disposed;

        public RedisLockHandle(IDatabase database, string key, string value)
        {
            _database = database;
            _key = key;
            _value = value;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                var script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
                await _database.ScriptEvaluateAsync(script, new RedisKey[] { _key }, new RedisValue[] { _value })
                    .ConfigureAwait(false);
            }
        }
    }
}
