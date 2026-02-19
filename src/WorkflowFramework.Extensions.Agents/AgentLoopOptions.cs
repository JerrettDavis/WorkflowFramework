namespace WorkflowFramework.Extensions.Agents;

/// <summary>Options for configuring an AgentLoopStep.</summary>
public sealed class AgentLoopOptions
{
    /// <summary>Max iterations. Default 10.</summary>
    public int MaxIterations { get; set; } = 10;
    /// <summary>System prompt.</summary>
    public string? SystemPrompt { get; set; }
    /// <summary>Context sources.</summary>
    public IList<IContextSource> ContextSources { get; set; } = new List<IContextSource>();
    /// <summary>Hook pipeline.</summary>
    public HookPipeline? Hooks { get; set; }
    /// <summary>Step name.</summary>
    public string? StepName { get; set; }
    /// <summary>Context manager.</summary>
    public IContextManager? ContextManager { get; set; }
    /// <summary>Auto-compact when over threshold. Default true.</summary>
    public bool AutoCompact { get; set; } = true;
    /// <summary>Max context tokens. Default 100000.</summary>
    public int MaxContextTokens { get; set; } = 100000;
    /// <summary>Compaction strategy.</summary>
    public ICompactionStrategy? CompactionStrategy { get; set; }
    /// <summary>Focus instructions for compaction.</summary>
    public string? CompactionFocusInstructions { get; set; }
    /// <summary>Checkpoint store.</summary>
    public ICheckpointStore? CheckpointStore { get; set; }
    /// <summary>Checkpoint interval (iterations). Default 1.</summary>
    public int CheckpointInterval { get; set; } = 1;
}
