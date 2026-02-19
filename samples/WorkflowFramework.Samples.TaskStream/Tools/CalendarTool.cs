namespace WorkflowFramework.Samples.TaskStream.Tools;

/// <summary>Mock calendar tool for scheduling.</summary>
public sealed class CalendarTool : IAgentTool
{
    /// <inheritdoc />
    public string Name => "calendar";

    /// <inheritdoc />
    public string Description => "Check and create calendar events";

    /// <inheritdoc />
    public Task<string> ExecuteAsync(string input, CancellationToken ct = default) =>
        Task.FromResult($"✅ Calendar event created: \"{input}\". Invites sent to all participants.");
}
