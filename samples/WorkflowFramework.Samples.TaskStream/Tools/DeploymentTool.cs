namespace WorkflowFramework.Samples.TaskStream.Tools;

/// <summary>Mock deployment tool for CI/CD operations.</summary>
public sealed class DeploymentTool : IAgentTool
{
    /// <inheritdoc />
    public string Name => "deployment";

    /// <inheritdoc />
    public string Description => "Create PRs and trigger deployments";

    /// <inheritdoc />
    public Task<string> ExecuteAsync(string input, CancellationToken ct = default) =>
        Task.FromResult($"🚀 Deployment initiated: \"{input}\". PR #142 created, CI pipeline triggered. ETA: 8 minutes.");
}
