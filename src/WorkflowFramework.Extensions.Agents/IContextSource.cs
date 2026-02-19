namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Provides context documents for agent prompts.
/// </summary>
public interface IContextSource
{
    /// <summary>Gets the name of this context source.</summary>
    string Name { get; }

    /// <summary>
    /// Gets all context documents from this source.
    /// </summary>
    Task<IReadOnlyList<ContextDocument>> GetContextAsync(CancellationToken ct = default);
}
