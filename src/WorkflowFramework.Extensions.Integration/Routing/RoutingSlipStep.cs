using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Routing;

/// <summary>
/// Default implementation of <see cref="IRoutingSlip"/>.
/// </summary>
public sealed class RoutingSlip : IRoutingSlip
{
    private int _currentIndex;

    /// <summary>
    /// Initializes a new instance of <see cref="RoutingSlip"/>.
    /// </summary>
    /// <param name="itinerary">The ordered list of step names to visit.</param>
    public RoutingSlip(IEnumerable<string> itinerary)
    {
        Itinerary = itinerary?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(itinerary));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Itinerary { get; }

    /// <inheritdoc />
    public int CurrentIndex => _currentIndex;

    /// <inheritdoc />
    public string? CurrentStep => _currentIndex < Itinerary.Count ? Itinerary[_currentIndex] : null;

    /// <inheritdoc />
    public bool Advance()
    {
        if (_currentIndex < Itinerary.Count)
            _currentIndex++;
        return _currentIndex < Itinerary.Count;
    }
}

/// <summary>
/// Processes a message through a routing slip, executing each step in order.
/// </summary>
public sealed class RoutingSlipStep : IStep
{
    private readonly Func<IWorkflowContext, IRoutingSlip> _slipSelector;
    private readonly IDictionary<string, IStep> _stepRegistry;
    /// <summary>
    /// The property key used to store the routing slip on the workflow context.
    /// </summary>
    public const string RoutingSlipKey = "__RoutingSlip";

    /// <summary>
    /// Initializes a new instance of <see cref="RoutingSlipStep"/>.
    /// </summary>
    /// <param name="slipSelector">Function to get/create the routing slip from context.</param>
    /// <param name="stepRegistry">Registry mapping step names to step instances.</param>
    public RoutingSlipStep(
        Func<IWorkflowContext, IRoutingSlip> slipSelector,
        IDictionary<string, IStep> stepRegistry)
    {
        _slipSelector = slipSelector ?? throw new ArgumentNullException(nameof(slipSelector));
        _stepRegistry = stepRegistry ?? throw new ArgumentNullException(nameof(stepRegistry));
    }

    /// <inheritdoc />
    public string Name => "RoutingSlip";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var slip = _slipSelector(context);
        context.Properties[RoutingSlipKey] = slip;

        while (slip.CurrentStep != null)
        {
            if (!_stepRegistry.TryGetValue(slip.CurrentStep, out var step))
                throw new InvalidOperationException($"Step '{slip.CurrentStep}' not found in routing slip registry.");

            await step.ExecuteAsync(context).ConfigureAwait(false);
            if (context.IsAborted) break;
            slip.Advance();
        }
    }
}
