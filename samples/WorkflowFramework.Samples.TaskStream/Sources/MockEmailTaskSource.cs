using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Sources;

/// <summary>
/// Returns hardcoded sample "emails" with task content for demonstration.
/// </summary>
public sealed class MockEmailTaskSource : ITaskSource
{
    /// <inheritdoc />
    public string Name => "Email";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceMessage> GetMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new SourceMessage
        {
            Source = "email",
            RawContent = "Hi team, please review the Q4 budget report and send feedback by Friday.",
            Metadata = { ["from"] = "boss@company.com", ["subject"] = "Q4 Budget Review" }
        };

        yield return new SourceMessage
        {
            Source = "email",
            RawContent = "Reminder: book the conference room for Wednesday's all-hands meeting.",
            Metadata = { ["from"] = "admin@company.com", ["subject"] = "Meeting Room" }
        };

        await Task.Yield();
    }
}
