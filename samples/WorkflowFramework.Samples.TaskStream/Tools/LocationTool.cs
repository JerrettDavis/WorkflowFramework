namespace WorkflowFramework.Samples.TaskStream.Tools;

/// <summary>Mock location tool for finding nearby places.</summary>
public sealed class LocationTool : IAgentTool
{
    /// <inheritdoc />
    public string Name => "location";

    /// <inheritdoc />
    public string Description => "Find nearby stores and locations";

    /// <inheritdoc />
    public Task<string> ExecuteAsync(string input, CancellationToken ct = default) =>
        Task.FromResult($"📍 Nearby options for '{input}': Target (0.8 mi), Walmart (1.2 mi), Local Shop (0.3 mi)");
}
