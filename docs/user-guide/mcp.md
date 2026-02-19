# MCP Server Integration

The `WorkflowFramework.Extensions.Agents.Mcp` package connects to [Model Context Protocol](https://modelcontextprotocol.io/) servers, exposing their tools and resources as native `IToolProvider` and `IContextSource` instances.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Agents.Mcp
```

## Key Types

| Type | Purpose |
|------|---------|
| `IMcpTransport` | Transport layer (stdio or HTTP) |
| `StdioMcpTransport` | Communicates via stdin/stdout of a child process |
| `HttpMcpTransport` | Communicates via streamable HTTP with SSE |
| `McpClient` | Protocol client with typed methods |
| `McpToolProvider` | Wraps `McpClient` as `IToolProvider` |
| `McpResourceProvider` | Wraps `McpClient` as `IContextSource` |
| `McpServerConfig` | Server connection configuration |

## IMcpTransport

Two built-in transports:

```csharp
public interface IMcpTransport : IDisposable
{
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task SendAsync(McpJsonRpcMessage message, CancellationToken ct = default);
    Task<McpJsonRpcMessage> ReceiveAsync(CancellationToken ct = default);
}
```

**StdioMcpTransport** — Launches a child process and communicates via stdin/stdout (one JSON-RPC message per line):

```csharp
var transport = new StdioMcpTransport(
    command: "npx",
    args: new[] { "-y", "@modelcontextprotocol/server-filesystem", "/home/user/docs" },
    env: new Dictionary<string, string> { ["NODE_ENV"] = "production" }
);
```

**HttpMcpTransport** — Posts JSON-RPC to an HTTP endpoint, parses SSE or plain JSON responses:

```csharp
var transport = new HttpMcpTransport(
    url: "https://mcp.example.com/rpc",
    headers: new Dictionary<string, string> { ["Authorization"] = "Bearer token123" }
);
```

## McpClient

The protocol client handles the MCP handshake (initialize → initialized) and provides typed methods:

```csharp
using var client = new McpClient(transport, "filesystem");

// Connect and handshake
await client.ConnectAsync();

// List available tools
IReadOnlyList<McpToolInfo> tools = await client.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
    // tool.InputSchema contains the JSON Schema string
}

// Call a tool
McpToolCallResult result = await client.CallToolAsync(
    "read_file",
    """{"path": "/home/user/docs/readme.md"}"""
);
Console.WriteLine(result.Content);

// List and read resources
IReadOnlyList<McpResourceInfo> resources = await client.ListResourcesAsync();
McpResourceContent content = await client.ReadResourceAsync(resources[0].Uri);
Console.WriteLine(content.Text);

// Disconnect
await client.DisconnectAsync();
```

## McpToolProvider

Wraps an `McpClient` as an `IToolProvider` so MCP tools appear in the `ToolRegistry`:

```csharp
var transport = new StdioMcpTransport("npx", new[] { "-y", "@mcp/server-github" });
var client = new McpClient(transport, "github");
await client.ConnectAsync();

var toolProvider = new McpToolProvider(client);
registry.Register(toolProvider);

// Now "github" tools are available in agent loops
var tools = await registry.ListAllToolsAsync();
// Each tool's Metadata["source"] = "mcp:github"
```

## McpResourceProvider

Wraps an `McpClient` as an `IContextSource` for resources:

```csharp
var resourceProvider = new McpResourceProvider(client);

// Use as context source in agent loops
var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, options =>
    {
        options.ContextSources.Add(resourceProvider);
    })
    .Build();
```

Resources are fetched and injected into the agent prompt as `ContextDocument` objects.

## McpServerConfig

Declarative configuration for MCP servers:

```csharp
// Stdio server
var stdioConfig = new McpServerConfig
{
    Name = "filesystem",
    Transport = "stdio",
    Command = "npx",
    Args = new[] { "-y", "@modelcontextprotocol/server-filesystem", "/data" },
    Env = new Dictionary<string, string> { ["HOME"] = "/home/user" }
};

// HTTP server
var httpConfig = new McpServerConfig
{
    Name = "remote-api",
    Transport = "http",
    Url = "https://mcp.example.com/rpc",
    Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer secret" }
};
```

## DI Registration

Register MCP servers via dependency injection with `AddMcpServer()`:

```csharp
services.AddMcpServer(new McpServerConfig
{
    Name = "filesystem",
    Transport = "stdio",
    Command = "npx",
    Args = new[] { "-y", "@modelcontextprotocol/server-filesystem", "/data" }
});

services.AddAgentTooling(); // Auto-discovers MCP tool providers
```

`AddMcpServer()` registers both an `IToolProvider` and an `IContextSource` for the server.

## Builder Extensions

```csharp
// Call a specific MCP tool in a workflow step
var workflow = new WorkflowBuilder()
    .CallMcpTool("filesystem", "read_file", """{"path":"{FilePath}"}""", registry)
    .Build();
```

## Complete Example

```csharp
// 1. Configure MCP servers
var transport = new StdioMcpTransport(
    "npx", new[] { "-y", "@modelcontextprotocol/server-filesystem", "/workspace" });
using var client = new McpClient(transport, "filesystem");
await client.ConnectAsync();

// 2. Set up registry with MCP + local tools
var registry = new ToolRegistry();
registry.Register(new McpToolProvider(client));
registry.Register(new CustomToolProvider());

// 3. Build agent workflow with MCP resources as context
var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, options =>
    {
        options.SystemPrompt = "You have access to the filesystem. Help the user manage their files.";
        options.ContextSources.Add(new McpResourceProvider(client));
        options.MaxIterations = 20;
    })
    .Build();

await workflow.RunAsync(context);
```
