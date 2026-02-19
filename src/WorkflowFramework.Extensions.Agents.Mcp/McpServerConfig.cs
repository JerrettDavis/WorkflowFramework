namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// Configuration for an MCP server connection.
/// </summary>
public sealed class McpServerConfig
{
    /// <summary>Gets or sets the server name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the transport type ("stdio" or "http").</summary>
    public string Transport { get; set; } = "stdio";

    /// <summary>Gets or sets the command to launch (for stdio transport).</summary>
    public string? Command { get; set; }

    /// <summary>Gets or sets the command arguments (for stdio transport).</summary>
    public string[]? Args { get; set; }

    /// <summary>Gets or sets the URL (for http transport).</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets HTTP headers (for http transport).</summary>
    public IDictionary<string, string>? Headers { get; set; }

    /// <summary>Gets or sets environment variables (for stdio transport).</summary>
    public IDictionary<string, string>? Env { get; set; }
}
