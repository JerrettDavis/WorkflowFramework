namespace WorkflowFramework.Extensions.Integration.Composition;

/// <summary>
/// Stateful orchestration that maintains state across multiple steps/interactions.
/// </summary>
public sealed class ProcessManagerStep : IStep
{
    private readonly Func<IWorkflowContext, string> _stateSelector;
    private readonly IDictionary<string, IStep> _stateHandlers;
    private readonly int _maxTransitions;
    /// <summary>
    /// The property key used to store the current process state.
    /// </summary>
    public const string StateKey = "__ProcessManagerState";

    /// <summary>
    /// Initializes a new instance of <see cref="ProcessManagerStep"/>.
    /// </summary>
    /// <param name="stateSelector">Function to determine current state from context.</param>
    /// <param name="stateHandlers">Map of state names to handler steps.</param>
    /// <param name="maxTransitions">Maximum state transitions to prevent infinite loops.</param>
    public ProcessManagerStep(
        Func<IWorkflowContext, string> stateSelector,
        IDictionary<string, IStep> stateHandlers,
        int maxTransitions = 100)
    {
        _stateSelector = stateSelector ?? throw new ArgumentNullException(nameof(stateSelector));
        _stateHandlers = stateHandlers ?? throw new ArgumentNullException(nameof(stateHandlers));
        _maxTransitions = maxTransitions;
    }

    /// <inheritdoc />
    public string Name => "ProcessManager";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var transitions = 0;

        while (transitions < _maxTransitions)
        {
            var state = _stateSelector(context);
            context.Properties[StateKey] = state;

            if (!_stateHandlers.TryGetValue(state, out var handler))
                break; // Terminal state

            await handler.ExecuteAsync(context).ConfigureAwait(false);
            if (context.IsAborted) break;
            transitions++;

            // Check if state changed
            var newState = _stateSelector(context);
            if (newState == state)
                break; // No state change, done
        }
    }
}
