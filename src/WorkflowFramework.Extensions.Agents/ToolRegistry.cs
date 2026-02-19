namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Aggregates multiple <see cref="IToolProvider"/> instances and provides unified tool access.
/// Last-registered provider wins when tool names conflict.
/// </summary>
public sealed class ToolRegistry
{
    private readonly List<IToolProvider> _providers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Registers a tool provider.
    /// </summary>
    public void Register(IToolProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        lock (_lock)
        {
            _providers.Add(provider);
        }
    }

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    public IReadOnlyList<IToolProvider> Providers
    {
        get
        {
            lock (_lock)
            {
                return _providers.ToArray();
            }
        }
    }

    /// <summary>
    /// Lists all tools from all registered providers. Last-registered wins on name conflicts.
    /// </summary>
    public async Task<IReadOnlyList<ToolDefinition>> ListAllToolsAsync(CancellationToken ct = default)
    {
        var tools = new Dictionary<string, ToolDefinition>();
        IToolProvider[] providers;
        lock (_lock)
        {
            providers = _providers.ToArray();
        }

        foreach (var provider in providers)
        {
            var providerTools = await provider.ListToolsAsync(ct).ConfigureAwait(false);
            foreach (var tool in providerTools)
            {
                tools[tool.Name] = tool;
            }
        }

        return tools.Values.ToArray();
    }

    /// <summary>
    /// Invokes a tool by name. Searches providers in reverse order (last-registered first).
    /// </summary>
    public async Task<ToolResult> InvokeAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        if (toolName == null) throw new ArgumentNullException(nameof(toolName));
        if (argumentsJson == null) throw new ArgumentNullException(nameof(argumentsJson));

        IToolProvider[] providers;
        lock (_lock)
        {
            providers = _providers.ToArray();
        }

        // Search in reverse order (last-registered wins)
        for (int i = providers.Length - 1; i >= 0; i--)
        {
            var tools = await providers[i].ListToolsAsync(ct).ConfigureAwait(false);
            foreach (var tool in tools)
            {
                if (tool.Name == toolName)
                {
                    return await providers[i].InvokeToolAsync(toolName, argumentsJson, ct).ConfigureAwait(false);
                }
            }
        }

        throw new InvalidOperationException($"Tool '{toolName}' not found in any registered provider.");
    }
}
