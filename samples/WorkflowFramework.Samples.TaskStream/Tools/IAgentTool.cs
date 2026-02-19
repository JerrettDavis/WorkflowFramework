namespace WorkflowFramework.Samples.TaskStream.Tools;

/// <summary>
/// Represents a tool available to the AI agent for task execution.
/// </summary>
public interface IAgentTool
{
    /// <summary>Gets the tool name.</summary>
    string Name { get; }

    /// <summary>Gets the tool description.</summary>
    string Description { get; }

    /// <summary>Executes the tool with the given input.</summary>
    Task<string> ExecuteAsync(string input, CancellationToken ct = default);
}
