namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Represents the result of a tool invocation.
/// </summary>
public sealed class ToolResult
{
    /// <summary>Gets or sets the content returned by the tool.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the tool invocation resulted in an error.</summary>
    public bool IsError { get; set; }

    /// <summary>Gets or sets arbitrary metadata about the result.</summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
