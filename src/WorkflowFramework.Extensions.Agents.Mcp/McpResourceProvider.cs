namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// Wraps an <see cref="McpClient"/> as an <see cref="IContextSource"/> for resources.
/// </summary>
public sealed class McpResourceProvider : IContextSource
{
    private readonly McpClient _client;

    /// <summary>
    /// Initializes a new instance of <see cref="McpResourceProvider"/>.
    /// </summary>
    public McpResourceProvider(McpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public string Name => $"mcp:{_client.ServerName}";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContextDocument>> GetContextAsync(CancellationToken ct = default)
    {
        var resources = await _client.ListResourcesAsync(ct).ConfigureAwait(false);
        var docs = new List<ContextDocument>();
        foreach (var resource in resources)
        {
            var content = await _client.ReadResourceAsync(resource.Uri, ct).ConfigureAwait(false);
            docs.Add(new ContextDocument
            {
                Name = resource.Name,
                Content = content.Text,
                Source = resource.Uri,
                Metadata = new Dictionary<string, string>
                {
                    ["mimeType"] = content.MimeType ?? "text/plain",
                    ["server"] = _client.ServerName
                }
            });
        }
        return docs;
    }
}
