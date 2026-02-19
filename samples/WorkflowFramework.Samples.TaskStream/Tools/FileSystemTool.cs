namespace WorkflowFramework.Samples.TaskStream.Tools;

/// <summary>Mock file system tool.</summary>
public sealed class FileSystemTool : IAgentTool
{
    /// <inheritdoc />
    public string Name => "filesystem";

    /// <inheritdoc />
    public string Description => "Read and write files";

    /// <inheritdoc />
    public Task<string> ExecuteAsync(string input, CancellationToken ct = default) =>
        Task.FromResult($"📄 File operation completed: \"{input}\". File saved successfully.");
}
