using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// MCP transport using streamable HTTP (POST with SSE responses).
/// </summary>
public sealed class HttpMcpTransport : IMcpTransport
{
    private readonly string _url;
    private readonly IDictionary<string, string>? _headers;
    private HttpClient? _httpClient;
    private readonly ConcurrentQueue<McpJsonRpcMessage> _receiveQueue = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="HttpMcpTransport"/>.
    /// </summary>
    public HttpMcpTransport(string url, IDictionary<string, string>? headers = null)
    {
        _url = url ?? throw new ArgumentNullException(nameof(url));
        _headers = headers;
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken ct = default)
    {
        _httpClient = new HttpClient();
        if (_headers != null)
        {
            foreach (var kvp in _headers)
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _httpClient?.Dispose();
        _httpClient = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendAsync(McpJsonRpcMessage message, CancellationToken ct = default)
    {
        if (_httpClient == null) throw new InvalidOperationException("Transport not connected.");
        var json = JsonSerializer.Serialize(message);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Parse SSE response
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var lines = responseBody.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("data:", StringComparison.Ordinal))
            {
                var data = trimmed.Substring(5).Trim();
                if (!string.IsNullOrEmpty(data) && data != "[DONE]")
                {
                    var msg = JsonSerializer.Deserialize<McpJsonRpcMessage>(data);
                    if (msg != null)
                    {
                        _receiveQueue.Enqueue(msg);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(trimmed) && trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                // Plain JSON response
                var msg = JsonSerializer.Deserialize<McpJsonRpcMessage>(trimmed);
                if (msg != null)
                {
                    _receiveQueue.Enqueue(msg);
                }
            }
        }
    }

    /// <inheritdoc />
    public Task<McpJsonRpcMessage> ReceiveAsync(CancellationToken ct = default)
    {
        if (_receiveQueue.TryDequeue(out var message))
        {
            return Task.FromResult(message);
        }
        throw new InvalidOperationException("No messages available. Call SendAsync first for HTTP transport.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _httpClient?.Dispose();
        }
    }
}
