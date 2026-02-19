namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// Wraps an <see cref="McpClient"/> as an <see cref="IToolProvider"/>.
/// </summary>
public sealed class McpToolProvider : IToolProvider
{
    private readonly McpClient _client;

    /// <summary>
    /// Initializes a new instance of <see cref="McpToolProvider"/>.
    /// </summary>
    public McpToolProvider(McpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
    {
        var mcpTools = await _client.ListToolsAsync(ct).ConfigureAwait(false);
        var tools = new List<ToolDefinition>();
        foreach (var t in mcpTools)
        {
            tools.Add(new ToolDefinition
            {
                Name = t.Name,
                Description = t.Description,
                ParametersSchema = t.InputSchema,
                Metadata = new Dictionary<string, string> { ["source"] = $"mcp:{_client.ServerName}" }
            });
        }
        return tools;
    }

    /// <inheritdoc />
    public async Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        var result = await _client.CallToolAsync(toolName, argumentsJson, ct).ConfigureAwait(false);
        return new ToolResult
        {
            Content = result.Content,
            IsError = result.IsError
        };
    }
}
