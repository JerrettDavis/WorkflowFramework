namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Represents a routing slip that defines a sequence of processing steps.
/// </summary>
public interface IRoutingSlip
{
    /// <summary>
    /// Gets the ordered list of step names/endpoints to visit.
    /// </summary>
    IReadOnlyList<string> Itinerary { get; }

    /// <summary>
    /// Gets the index of the current step in the itinerary.
    /// </summary>
    int CurrentIndex { get; }

    /// <summary>
    /// Advances to the next step in the itinerary.
    /// </summary>
    /// <returns>True if there is a next step; false if the itinerary is complete.</returns>
    bool Advance();

    /// <summary>
    /// Gets the current step name, or null if the itinerary is complete.
    /// </summary>
    string? CurrentStep { get; }
}
