namespace WorkflowFramework.Samples.TaskStream.Tools;

/// <summary>Mock web search tool.</summary>
public sealed class WebSearchTool : IAgentTool
{
    /// <inheritdoc />
    public string Name => "web_search";

    /// <inheritdoc />
    public string Description => "Search the web for information";

    /// <inheritdoc />
    public Task<string> ExecuteAsync(string input, CancellationToken ct = default) =>
        Task.FromResult($"[Search Results for '{input}']: Found 3 relevant results. Top result: \"{input} - comprehensive guide and best practices.\"");
}
