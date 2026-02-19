namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// In-memory context manager with auto-compaction support.
/// </summary>
public sealed class DefaultContextManager : IContextManager
{
    private readonly List<ConversationMessage> _messages = new();
    private readonly ITokenEstimator _estimator;

    /// <summary>
    /// Initializes a new instance of <see cref="DefaultContextManager"/>.
    /// </summary>
    public DefaultContextManager(ITokenEstimator? estimator = null)
    {
        _estimator = estimator ?? new DefaultTokenEstimator();
    }

    /// <inheritdoc />
    public void AddMessage(ConversationMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        _messages.Add(message);
    }

    /// <inheritdoc />
    public void AddToolCall(string toolName, string args, string result)
    {
        _messages.Add(new ConversationMessage
        {
            Role = ConversationRole.Tool,
            Content = $"Tool '{toolName}' called with {args}: {result}",
            Metadata = new Dictionary<string, string>
            {
                ["toolName"] = toolName,
                ["args"] = args
            }
        });
    }

    /// <inheritdoc />
    public IReadOnlyList<ConversationMessage> GetMessages() => _messages.AsReadOnly();

    /// <inheritdoc />
    public int EstimateTokenCount()
    {
        int total = 0;
        foreach (var msg in _messages)
        {
            total += _estimator.EstimateTokens(msg.Content);
        }
        return total;
    }

    /// <inheritdoc />
    public async Task<CompactionResult> CompactAsync(CompactionOptions options, CancellationToken ct = default)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        var originalCount = _messages.Count;
        var originalTokens = EstimateTokenCount();

        var strategy = options.Strategy ?? new SlidingWindowCompactionStrategy();

        // Separate system messages if preserving
        var systemMessages = new List<ConversationMessage>();
        var nonSystem = new List<ConversationMessage>();

        foreach (var msg in _messages)
        {
            if (options.PreserveSystemMessages && msg.Role == ConversationRole.System)
                systemMessages.Add(msg);
            else
                nonSystem.Add(msg);
        }

        // Preserve recent messages
        var recentCount = Math.Min(options.PreserveRecentCount, nonSystem.Count);
        var toCompact = nonSystem.Count > recentCount
            ? nonSystem.GetRange(0, nonSystem.Count - recentCount)
            : new List<ConversationMessage>();
        var recent = nonSystem.Count > recentCount
            ? nonSystem.GetRange(nonSystem.Count - recentCount, recentCount)
            : new List<ConversationMessage>(nonSystem);

        string summary = string.Empty;
        if (toCompact.Count > 0)
        {
            summary = await strategy.SummarizeAsync(toCompact, options, ct).ConfigureAwait(false);
        }

        // Rebuild messages
        _messages.Clear();
        _messages.AddRange(systemMessages);

        if (!string.IsNullOrEmpty(summary))
        {
            _messages.Add(new ConversationMessage
            {
                Role = ConversationRole.System,
                Content = summary,
                IsCompacted = true
            });
        }

        _messages.AddRange(recent);

        return new CompactionResult
        {
            OriginalMessageCount = originalCount,
            CompactedMessageCount = _messages.Count,
            OriginalTokenEstimate = originalTokens,
            CompactedTokenEstimate = EstimateTokenCount(),
            Summary = summary
        };
    }

    /// <inheritdoc />
    public ContextSnapshot CreateSnapshot()
    {
        return new ContextSnapshot
        {
            Messages = _messages.Select(m => new ConversationMessage
            {
                Role = m.Role,
                Content = m.Content,
                Timestamp = m.Timestamp,
                Metadata = new Dictionary<string, string>(m.Metadata),
                IsCompacted = m.IsCompacted
            }).ToList(),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc />
    public void RestoreSnapshot(ContextSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        _messages.Clear();
        _messages.AddRange(snapshot.Messages);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _messages.Clear();
    }
}


