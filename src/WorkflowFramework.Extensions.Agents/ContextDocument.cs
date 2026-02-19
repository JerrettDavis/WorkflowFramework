namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// A document providing context for agent prompts.
/// </summary>
public sealed class ContextDocument
{
    /// <summary>Gets or sets the document name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the document content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the source of the document.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets arbitrary metadata.</summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
