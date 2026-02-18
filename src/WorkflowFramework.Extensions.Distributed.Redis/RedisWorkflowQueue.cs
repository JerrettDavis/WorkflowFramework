using System.Text.Json;
using StackExchange.Redis;

namespace WorkflowFramework.Extensions.Distributed.Redis;

/// <summary>
/// Redis-based implementation of <see cref="IWorkflowQueue"/>.
/// </summary>
public sealed class RedisWorkflowQueue : IWorkflowQueue
{
    private readonly IDatabase _database;
    private readonly string _queueKey;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Initializes a new instance of <see cref="RedisWorkflowQueue"/>.
    /// </summary>
    /// <param name="database">The Redis database.</param>
    /// <param name="queueKey">The Redis key for the queue.</param>
    public RedisWorkflowQueue(IDatabase database, string queueKey = "workflow:queue")
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _queueKey = queueKey;
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(WorkflowQueueItem item, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(item, JsonOptions);
        await _database.ListRightPushAsync(_queueKey, json).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkflowQueueItem?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var json = await _database.ListLeftPopAsync(_queueKey).ConfigureAwait(false);
        if (json.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<WorkflowQueueItem>((string)json!, JsonOptions);
    }

    /// <inheritdoc />
    public async Task<int> GetLengthAsync(CancellationToken cancellationToken = default) =>
        (int)await _database.ListLengthAsync(_queueKey).ConfigureAwait(false);
}
