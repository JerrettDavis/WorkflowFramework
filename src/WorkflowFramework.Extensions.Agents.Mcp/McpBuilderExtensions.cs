using WorkflowFramework.Builder;

namespace WorkflowFramework.Extensions.Agents.Mcp;

/// <summary>
/// Extension methods for <see cref="IWorkflowBuilder"/> to add MCP tool call steps.
/// </summary>
public static class McpBuilderExtensions
{
    /// <summary>
    /// Adds a step that calls an MCP tool.
    /// </summary>
    public static IWorkflowBuilder CallMcpTool(
        this IWorkflowBuilder builder,
        string serverName,
        string toolName,
        string argumentsTemplate,
        ToolRegistry? registry = null)
    {
        // Uses ToolCallStep with a naming convention for MCP tools
        var stepName = $"McpTool.{serverName}.{toolName}";
        if (registry != null)
        {
            return builder.Step(new ToolCallStep(registry, toolName, argumentsTemplate, stepName));
        }
        // When no registry is provided, create a placeholder step that expects registry in context
        return builder.Step(new ToolCallStep(
            new ToolRegistry(), toolName, argumentsTemplate, stepName));
    }
}
