namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Describes a tool that can be invoked by an agent.
/// </summary>
public sealed class ToolDefinition
{
    /// <summary>Gets or sets the tool name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the tool description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON schema for the tool's parameters.</summary>
    public string? ParametersSchema { get; set; }

    /// <summary>Gets or sets arbitrary metadata about the tool.</summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
