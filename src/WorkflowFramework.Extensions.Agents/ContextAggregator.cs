using System.Text;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Combines multiple <see cref="IContextSource"/> instances into a single prompt section.
/// </summary>
public sealed class ContextAggregator
{
    private readonly List<IContextSource> _sources = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ContextAggregator"/>.
    /// </summary>
    public ContextAggregator()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ContextAggregator"/> with the given sources.
    /// </summary>
    public ContextAggregator(IEnumerable<IContextSource> sources)
    {
        if (sources == null) throw new ArgumentNullException(nameof(sources));
        _sources.AddRange(sources);
    }

    /// <summary>
    /// Adds a context source.
    /// </summary>
    public void Add(IContextSource source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        _sources.Add(source);
    }

    /// <summary>Gets the sources in this aggregator.</summary>
    public IReadOnlyList<IContextSource> Sources => _sources.AsReadOnly();

    /// <summary>
    /// Gets all context documents from all sources.
    /// </summary>
    public async Task<IReadOnlyList<ContextDocument>> GetAllContextAsync(CancellationToken ct = default)
    {
        var all = new List<ContextDocument>();
        foreach (var source in _sources)
        {
            var docs = await source.GetContextAsync(ct).ConfigureAwait(false);
            all.AddRange(docs);
        }
        return all.AsReadOnly();
    }

    /// <summary>
    /// Builds a combined prompt section from all context documents.
    /// </summary>
    public async Task<string> BuildContextPromptAsync(CancellationToken ct = default)
    {
        var docs = await GetAllContextAsync(ct).ConfigureAwait(false);
        if (docs.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("## Context");
        foreach (var doc in docs)
        {
            sb.AppendLine($"### {doc.Name}");
            if (!string.IsNullOrEmpty(doc.Source))
                sb.AppendLine($"Source: {doc.Source}");
            sb.AppendLine(doc.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
