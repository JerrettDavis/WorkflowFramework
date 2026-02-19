namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Provides unified tool discovery and invocation.
/// </summary>
public interface IToolProvider
{
    /// <summary>
    /// Lists all tools available from this provider.
    /// </summary>
    Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default);

    /// <summary>
    /// Invokes a tool by name with the given JSON arguments.
    /// </summary>
    Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct = default);
}
