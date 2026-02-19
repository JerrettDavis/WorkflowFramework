using System.Text.Json;

namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// MCP protocol client. Manages transport lifecycle and provides typed methods for MCP operations.
/// </summary>
public sealed class McpClient : IDisposable
{
    private readonly IMcpTransport _transport;
    private readonly string _serverName;
    private int _nextId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="McpClient"/>.
    /// </summary>
    public McpClient(IMcpTransport transport, string serverName)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _serverName = serverName ?? throw new ArgumentNullException(nameof(serverName));
    }

    /// <summary>Gets the server name.</summary>
    public string ServerName => _serverName;

    /// <summary>
    /// Connects to the MCP server and performs the initialize handshake.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _transport.ConnectAsync(ct).ConfigureAwait(false);

        // Send initialize request
        var initRequest = new McpJsonRpcMessage
        {
            Id = NextId(),
            Method = "initialize",
            Params = JsonSerializer.SerializeToElement(new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "WorkflowFramework", version = "1.0.0" }
            })
        };
        await _transport.SendAsync(initRequest, ct).ConfigureAwait(false);
        var initResponse = await _transport.ReceiveAsync(ct).ConfigureAwait(false);

        if (initResponse.Error != null)
            throw new InvalidOperationException($"MCP initialize failed: {initResponse.Error.Message}");

        // Send initialized notification
        var initializedNotification = new McpJsonRpcMessage
        {
            Method = "notifications/initialized"
        };
        await _transport.SendAsync(initializedNotification, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Disconnects from the MCP server.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _transport.DisconnectAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists tools available on the MCP server.
    /// </summary>
    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
    {
        var request = new McpJsonRpcMessage
        {
            Id = NextId(),
            Method = "tools/list",
            Params = JsonSerializer.SerializeToElement(new { })
        };
        await _transport.SendAsync(request, ct).ConfigureAwait(false);
        var response = await _transport.ReceiveAsync(ct).ConfigureAwait(false);

        if (response.Error != null)
            throw new InvalidOperationException($"MCP tools/list failed: {response.Error.Message}");

        var tools = new List<McpToolInfo>();
        if (response.Result.HasValue)
        {
            var result = response.Result.Value;
            if (result.TryGetProperty("tools", out var toolsArray))
            {
                foreach (var toolElement in toolsArray.EnumerateArray())
                {
                    var tool = new McpToolInfo
                    {
                        Name = toolElement.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                        Description = toolElement.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty,
                        InputSchema = toolElement.TryGetProperty("inputSchema", out var s) ? s.GetRawText() : null
                    };
                    tools.Add(tool);
                }
            }
        }
        return tools;
    }

    /// <summary>
    /// Calls a tool on the MCP server.
    /// </summary>
    public async Task<McpToolCallResult> CallToolAsync(string name, string argumentsJson, CancellationToken ct = default)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (argumentsJson == null) throw new ArgumentNullException(nameof(argumentsJson));

        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        var request = new McpJsonRpcMessage
        {
            Id = NextId(),
            Method = "tools/call",
            Params = JsonSerializer.SerializeToElement(new { name, arguments = args })
        };
        await _transport.SendAsync(request, ct).ConfigureAwait(false);
        var response = await _transport.ReceiveAsync(ct).ConfigureAwait(false);

        if (response.Error != null)
            throw new InvalidOperationException($"MCP tools/call failed: {response.Error.Message}");

        var result = new McpToolCallResult();
        if (response.Result.HasValue)
        {
            var res = response.Result.Value;
            if (res.TryGetProperty("content", out var contentArray))
            {
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text))
                    {
                        result.Content += text.GetString();
                    }
                }
            }
            if (res.TryGetProperty("isError", out var isError))
            {
                result.IsError = isError.GetBoolean();
            }
        }
        return result;
    }

    /// <summary>
    /// Lists resources available on the MCP server.
    /// </summary>
    public async Task<IReadOnlyList<McpResourceInfo>> ListResourcesAsync(CancellationToken ct = default)
    {
        var request = new McpJsonRpcMessage
        {
            Id = NextId(),
            Method = "resources/list",
            Params = JsonSerializer.SerializeToElement(new { })
        };
        await _transport.SendAsync(request, ct).ConfigureAwait(false);
        var response = await _transport.ReceiveAsync(ct).ConfigureAwait(false);

        if (response.Error != null)
            throw new InvalidOperationException($"MCP resources/list failed: {response.Error.Message}");

        var resources = new List<McpResourceInfo>();
        if (response.Result.HasValue)
        {
            var result = response.Result.Value;
            if (result.TryGetProperty("resources", out var resourcesArray))
            {
                foreach (var resElement in resourcesArray.EnumerateArray())
                {
                    var resource = new McpResourceInfo
                    {
                        Uri = resElement.TryGetProperty("uri", out var u) ? u.GetString() ?? string.Empty : string.Empty,
                        Name = resElement.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                        Description = resElement.TryGetProperty("description", out var d) ? d.GetString() : null,
                        MimeType = resElement.TryGetProperty("mimeType", out var m) ? m.GetString() : null
                    };
                    resources.Add(resource);
                }
            }
        }
        return resources;
    }

    /// <summary>
    /// Reads a resource from the MCP server.
    /// </summary>
    public async Task<McpResourceContent> ReadResourceAsync(string uri, CancellationToken ct = default)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));

        var request = new McpJsonRpcMessage
        {
            Id = NextId(),
            Method = "resources/read",
            Params = JsonSerializer.SerializeToElement(new { uri })
        };
        await _transport.SendAsync(request, ct).ConfigureAwait(false);
        var response = await _transport.ReceiveAsync(ct).ConfigureAwait(false);

        if (response.Error != null)
            throw new InvalidOperationException($"MCP resources/read failed: {response.Error.Message}");

        var content = new McpResourceContent { Uri = uri };
        if (response.Result.HasValue)
        {
            var result = response.Result.Value;
            if (result.TryGetProperty("contents", out var contentsArray))
            {
                foreach (var item in contentsArray.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text))
                    {
                        content.Text += text.GetString();
                    }
                    if (item.TryGetProperty("mimeType", out var mime))
                    {
                        content.MimeType = mime.GetString();
                    }
                }
            }
        }
        return content;
    }

    private int NextId() => System.Threading.Interlocked.Increment(ref _nextId);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _transport.Dispose();
        }
    }
}

/// <summary>Tool info from MCP server.</summary>
public sealed class McpToolInfo
{
    /// <summary>Gets or sets the tool name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the tool description.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Gets or sets the input schema JSON.</summary>
    public string? InputSchema { get; set; }
}

/// <summary>Result of an MCP tool call.</summary>
public sealed class McpToolCallResult
{
    /// <summary>Gets or sets the content.</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>Gets or sets whether the call resulted in an error.</summary>
    public bool IsError { get; set; }
}

/// <summary>Resource info from MCP server.</summary>
public sealed class McpResourceInfo
{
    /// <summary>Gets or sets the resource URI.</summary>
    public string Uri { get; set; } = string.Empty;
    /// <summary>Gets or sets the resource name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the resource description.</summary>
    public string? Description { get; set; }
    /// <summary>Gets or sets the MIME type.</summary>
    public string? MimeType { get; set; }
}

/// <summary>Content of an MCP resource.</summary>
public sealed class McpResourceContent
{
    /// <summary>Gets or sets the resource URI.</summary>
    public string Uri { get; set; } = string.Empty;
    /// <summary>Gets or sets the text content.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Gets or sets the MIME type.</summary>
    public string? MimeType { get; set; }
}
