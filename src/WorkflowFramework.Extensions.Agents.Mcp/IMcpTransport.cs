namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// Transport layer for MCP JSON-RPC communication.
/// </summary>
public interface IMcpTransport : IDisposable
{
    /// <summary>Sends a JSON-RPC message.</summary>
    Task SendAsync(McpJsonRpcMessage message, CancellationToken ct = default);

    /// <summary>Receives the next JSON-RPC message.</summary>
    Task<McpJsonRpcMessage> ReceiveAsync(CancellationToken ct = default);

    /// <summary>Connects the transport.</summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Disconnects the transport.</summary>
    Task DisconnectAsync(CancellationToken ct = default);
}
