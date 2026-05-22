using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Internal;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;
using WfEvent = WorkflowFramework.Internal.WorkflowStatusMachine.WorkflowEvent;

namespace WorkflowFramework.Tests.TinyBDD.Core.StateMachine;

[Feature("WorkflowStatusMachine — advisory PatternKit StateMachine")]
public class WorkflowStatusMachineScenarios : TinyBddTestBase
{
    public WorkflowStatusMachineScenarios(ITestOutputHelper output) : base(output) { }

    // -------- happy-path legal transitions --------

    [Scenario("Pending transitions to Running on Start event"), Fact]
    public async Task PendingToRunningOnStart()
    {
        var state = WorkflowStatus.Pending;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Start);

        await Given("state=Pending, event=Start", () => (transitioned, state))
            .Then("state becomes Running", t =>
            {
                t.transitioned.Should().BeTrue();
                t.state.Should().Be(WorkflowStatus.Running);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Running transitions to Completed on Complete event"), Fact]
    public async Task RunningToCompletedOnComplete()
    {
        var state = WorkflowStatus.Running;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Complete);

        await Given("state=Running, event=Complete", () => (transitioned, state))
            .Then("state becomes Completed", t =>
            {
                t.transitioned.Should().BeTrue();
                t.state.Should().Be(WorkflowStatus.Completed);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Running transitions to Faulted on Fail event"), Fact]
    public async Task RunningToFaultedOnFail()
    {
        var state = WorkflowStatus.Running;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Fail);

        await Given("state=Running, event=Fail", () => (transitioned, state))
            .Then("state becomes Faulted", t =>
            {
                t.transitioned.Should().BeTrue();
                t.state.Should().Be(WorkflowStatus.Faulted);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Running transitions to Compensated on Compensate event"), Fact]
    public async Task RunningToCompensatedOnCompensate()
    {
        var state = WorkflowStatus.Running;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Compensate);

        await Given("state=Running, event=Compensate", () => (transitioned, state))
            .Then("state becomes Compensated", t =>
            {
                t.transitioned.Should().BeTrue();
                t.state.Should().Be(WorkflowStatus.Compensated);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Running transitions to Aborted on Abort event"), Fact]
    public async Task RunningToAbortedOnAbort()
    {
        var state = WorkflowStatus.Running;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Abort);

        await Given("state=Running, event=Abort", () => (transitioned, state))
            .Then("state becomes Aborted", t =>
            {
                t.transitioned.Should().BeTrue();
                t.state.Should().Be(WorkflowStatus.Aborted);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Running transitions to Suspended on Suspend event"), Fact]
    public async Task RunningToSuspendedOnSuspend()
    {
        var state = WorkflowStatus.Running;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Suspend);

        await Given("state=Running, event=Suspend", () => (transitioned, state))
            .Then("state becomes Suspended", t =>
            {
                t.transitioned.Should().BeTrue();
                t.state.Should().Be(WorkflowStatus.Suspended);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Suspended transitions to Running on Resume event"), Fact]
    public async Task SuspendedToRunningOnResume()
    {
        var state = WorkflowStatus.Suspended;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Resume);

        await Given("state=Suspended, event=Resume", () => (transitioned, state))
            .Then("state becomes Running", t =>
            {
                t.transitioned.Should().BeTrue();
                t.state.Should().Be(WorkflowStatus.Running);
                return true;
            })
            .AssertPassed();
    }

    // -------- full Compensating chain: Running→Compensated --------

    [Scenario("Full compensation path: Pending→Running→Compensated"), Fact]
    public async Task FullCompensationPath()
    {
        var state = WorkflowStatus.Pending;
        WorkflowStatusMachine.TryTransition(ref state, WfEvent.Start);
        var compensated = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Compensate);

        await Given("Pending→Running→Compensate path", () => (compensated, state))
            .Then("final state is Compensated", t =>
            {
                t.compensated.Should().BeTrue();
                t.state.Should().Be(WorkflowStatus.Compensated);
                return true;
            })
            .AssertPassed();
    }

    // -------- illegal / unregistered transitions --------

    [Scenario("Completed is a terminal state — further events are rejected"), Fact]
    public async Task CompletedIsTerminal()
    {
        var state = WorkflowStatus.Completed;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Start);

        await Given("state=Completed, event=Start", () => (transitioned, state))
            .Then("transition is rejected and state is unchanged", t =>
            {
                t.transitioned.Should().BeFalse();
                t.state.Should().Be(WorkflowStatus.Completed);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Faulted is a terminal state — further events are rejected"), Fact]
    public async Task FaultedIsTerminal()
    {
        var state = WorkflowStatus.Faulted;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Start);

        await Given("state=Faulted, event=Start", () => (transitioned, state))
            .Then("transition is rejected and state is unchanged", t =>
            {
                t.transitioned.Should().BeFalse();
                t.state.Should().Be(WorkflowStatus.Faulted);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Aborted is a terminal state — further events are rejected"), Fact]
    public async Task AbortedIsTerminal()
    {
        var state = WorkflowStatus.Aborted;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Complete);

        await Given("state=Aborted, event=Complete", () => (transitioned, state))
            .Then("transition is rejected and state is unchanged", t =>
            {
                t.transitioned.Should().BeFalse();
                t.state.Should().Be(WorkflowStatus.Aborted);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Pending does not accept Complete event directly"), Fact]
    public async Task PendingDoesNotAcceptComplete()
    {
        var state = WorkflowStatus.Pending;
        var transitioned = WorkflowStatusMachine.TryTransition(ref state, WfEvent.Complete);

        await Given("state=Pending, event=Complete", () => (transitioned, state))
            .Then("transition is rejected; state remains Pending", t =>
            {
                t.transitioned.Should().BeFalse();
                t.state.Should().Be(WorkflowStatus.Pending);
                return true;
            })
            .AssertPassed();
    }
}
