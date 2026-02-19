using Microsoft.Extensions.DependencyInjection;

namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// DI extension methods for MCP server registration.
/// </summary>
public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Registers an MCP server as a tool provider and context source.
    /// </summary>
    public static IServiceCollection AddMcpServer(this IServiceCollection services, McpServerConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        services.AddSingleton<IToolProvider>(sp =>
        {
            var transport = CreateTransport(config);
            var client = new McpClient(transport, config.Name);
            return new McpToolProvider(client);
        });

        services.AddSingleton<IContextSource>(sp =>
        {
            var transport = CreateTransport(config);
            var client = new McpClient(transport, config.Name);
            return new McpResourceProvider(client);
        });

        return services;
    }

    private static IMcpTransport CreateTransport(McpServerConfig config)
    {
        return config.Transport.ToLowerInvariant() switch
        {
            "stdio" => new StdioMcpTransport(
                config.Command ?? throw new InvalidOperationException("Command required for stdio transport."),
                config.Args,
                config.Env),
            "http" => new HttpMcpTransport(
                config.Url ?? throw new InvalidOperationException("URL required for http transport."),
                config.Headers),
            _ => throw new InvalidOperationException($"Unknown transport type: {config.Transport}")
        };
    }
}
