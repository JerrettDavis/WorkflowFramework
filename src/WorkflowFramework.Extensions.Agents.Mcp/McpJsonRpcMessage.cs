using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// Represents a JSON-RPC 2.0 message used by the MCP protocol.
/// </summary>
public sealed class McpJsonRpcMessage
{
    /// <summary>Gets or sets the JSON-RPC version. Always "2.0".</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>Gets or sets the message id for request/response correlation.</summary>
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    /// <summary>Gets or sets the method name (for requests/notifications).</summary>
    [JsonPropertyName("method")]
    public string? Method { get; set; }

    /// <summary>Gets or sets the parameters (for requests/notifications).</summary>
    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    /// <summary>Gets or sets the result (for responses).</summary>
    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    /// <summary>Gets or sets the error (for error responses).</summary>
    [JsonPropertyName("error")]
    public McpJsonRpcError? Error { get; set; }
}

/// <summary>
/// Represents a JSON-RPC error.
/// </summary>
public sealed class McpJsonRpcError
{
    /// <summary>Gets or sets the error code.</summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>Gets or sets the error message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets additional error data.</summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}
