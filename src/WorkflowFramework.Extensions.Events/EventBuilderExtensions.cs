using WorkflowFramework.Builder;

namespace WorkflowFramework.Extensions.Events;

/// <summary>
/// Fluent builder extensions for event steps.
/// </summary>
public static class EventBuilderExtensions
{
    /// <summary>Adds a publish-event step.</summary>
    public static IWorkflowBuilder PublishEvent(this IWorkflowBuilder builder, IEventBus eventBus, string eventType, string? name = null)
    {
        return builder.Step(new PublishEventStep(eventBus, ctx => new WorkflowEvent
        {
            EventType = eventType,
            CorrelationId = ctx.CorrelationId
        }, name));
    }

    /// <summary>Adds a wait-for-event step.</summary>
    public static IWorkflowBuilder WaitForEvent(this IWorkflowBuilder builder, IEventBus eventBus, string eventType, TimeSpan timeout, string? name = null)
    {
        return builder.Step(new WaitForEventStep(eventBus, eventType, ctx => ctx.CorrelationId, timeout, name));
    }
}
