using PatternKit.Behavioral.State;

namespace WorkflowFramework.Internal;

/// <summary>
/// Authoritative PatternKit <see cref="StateMachine{TState,TEvent}"/> that defines and
/// enforces legal <see cref="WorkflowStatus"/> transitions for <see cref="WorkflowEngine"/>.
/// </summary>
/// <remarks>
/// Promoted from advisory to authoritative in Phase F. The production <c>WorkflowEngine</c>
/// routes every status transition through <see cref="TryTransition"/>. Illegal transitions
/// cause <see cref="InvalidOperationException"/> to be thrown by the engine.
/// <list type="bullet">
///   <item>Single authoritative model of all allowed transitions.</item>
///   <item>Guards against logic errors that would produce an invalid status sequence.</item>
///   <item>Thread safety: the compiled machine is immutable. Callers pass <c>state</c> by ref.</item>
/// </list>
/// </remarks>
internal static class WorkflowStatusMachine
{
    /// <summary>Events that drive workflow status transitions.</summary>
    internal enum WorkflowEvent
    {
        Start,
        Complete,
        Fail,
        Compensate,
        Abort,
        Suspend,
        Resume
    }

    // Pre-built predicates using the correct 'in' parameter syntax for StateMachine<,>.Predicate
    private static bool IsStart(in WorkflowEvent e) => e == WorkflowEvent.Start;
    private static bool IsComplete(in WorkflowEvent e) => e == WorkflowEvent.Complete;
    private static bool IsFail(in WorkflowEvent e) => e == WorkflowEvent.Fail;
    private static bool IsCompensate(in WorkflowEvent e) => e == WorkflowEvent.Compensate;
    private static bool IsAbort(in WorkflowEvent e) => e == WorkflowEvent.Abort;
    private static bool IsSuspend(in WorkflowEvent e) => e == WorkflowEvent.Suspend;
    private static bool IsResume(in WorkflowEvent e) => e == WorkflowEvent.Resume;

    /// <summary>
    /// The shared, compiled state machine instance.
    /// Build once; share across all callers.
    /// </summary>
    internal static readonly StateMachine<WorkflowStatus, WorkflowEvent> Machine = BuildMachine();

    private static StateMachine<WorkflowStatus, WorkflowEvent> BuildMachine()
    {
        return StateMachine<WorkflowStatus, WorkflowEvent>.Create()
            // Pending → Running
            .InState(WorkflowStatus.Pending, s => s
                .When(IsStart).Permit(WorkflowStatus.Running).End())

            // Running → terminal states and Suspended
            .InState(WorkflowStatus.Running, s => s
                .When(IsComplete).Permit(WorkflowStatus.Completed).End()
                .When(IsFail).Permit(WorkflowStatus.Faulted).End()
                .When(IsCompensate).Permit(WorkflowStatus.Compensated).End()
                .When(IsAbort).Permit(WorkflowStatus.Aborted).End()
                .When(IsSuspend).Permit(WorkflowStatus.Suspended).End())

            // Suspended → Running (resume)
            .InState(WorkflowStatus.Suspended, s => s
                .When(IsResume).Permit(WorkflowStatus.Running).End())

            // Terminal states: Completed, Faulted, Aborted, Compensated have no exits.
            // (No transitions registered = TryTransition returns false for all events.)
            .Build();
    }

    /// <summary>
    /// Attempts to transition <paramref name="status"/> via <paramref name="event"/>.
    /// Returns <c>true</c> when the transition is legal and the state was updated.
    /// Returns <c>false</c> when no transition is defined — the <see cref="WorkflowEngine"/>
    /// treats this as an <see cref="InvalidOperationException"/>.
    /// </summary>
    internal static bool TryTransition(ref WorkflowStatus status, WorkflowEvent @event)
        => Machine.TryTransition(ref status, @event);
}
