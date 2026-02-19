namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Options for configuring agent tooling via DI.
/// </summary>
public sealed class AgentToolingOptions
{
    /// <summary>Gets or sets the maximum number of concurrent tool invocations.</summary>
    public int MaxToolConcurrency { get; set; } = 4;

    /// <summary>Gets or sets the default timeout for tool invocations.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
