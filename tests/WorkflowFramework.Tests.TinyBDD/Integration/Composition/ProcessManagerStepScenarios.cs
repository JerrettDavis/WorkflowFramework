using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Composition;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Composition;

[Feature("ProcessManagerStep — characterization (Phase G.2)")]
public class ProcessManagerStepScenarios : TinyBddTestBase
{
    public ProcessManagerStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("ProcessManagerStep Name returns 'ProcessManager'"), Fact]
    public async Task NameIsProcessManager()
    {
        var sut = new ProcessManagerStep(_ => "done", new Dictionary<string, IStep>());

        await Given("ProcessManagerStep instance", () => sut)
            .Then("Name is 'ProcessManager'", s =>
            {
                s.Name.Should().Be("ProcessManager");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("State without handler is treated as terminal state"), Fact]
    public async Task MissingHandlerIsTerminalState()
    {
        var callCount = 0;
        var ctx = new WorkflowContext();
        ctx.Properties[ProcessManagerStep.StateKey] = "terminal";

        var sut = new ProcessManagerStep(
            c => c.Properties.TryGetValue(ProcessManagerStep.StateKey, out var sv) ? (string?)sv ?? "terminal" : "terminal",
            new Dictionary<string, IStep>()); // no handlers — terminal immediately

        await sut.ExecuteAsync(ctx);

        await Given("call count in terminal-only process manager", () => callCount)
            .Then("no handlers ran (immediately terminal)", count =>
            {
                count.Should().Be(0);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Handler runs for initial state and stops when state does not change"), Fact]
    public async Task HandlerRunsAndStopsOnNoStateChange()
    {
        var handlerCallCount = 0;
        var ctx = new WorkflowContext();
        ctx.Properties[ProcessManagerStep.StateKey] = "active";

        var handler = Substitute.For<IStep>();
        handler.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { handlerCallCount++; return Task.CompletedTask; });

        // State selector always returns "active" — handler runs once then stops (same state)
        var sut = new ProcessManagerStep(
            _ => "active",
            new Dictionary<string, IStep> { ["active"] = handler });

        await sut.ExecuteAsync(ctx);

        await Given("call count for handler in non-transitioning state", () => handlerCallCount)
            .Then("handler ran exactly once then stopped (no state change)", count =>
            {
                count.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Handler transitions state and runs next state handler"), Fact]
    public async Task HandlerTransitionsStateAndRunsNextHandler()
    {
        var visited = new List<string>();
        var ctx = new WorkflowContext();
        ctx.Properties[ProcessManagerStep.StateKey] = "init";

        var initHandler = Substitute.For<IStep>();
        initHandler.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                visited.Add("init");
                ((IWorkflowContext)ci[0]).Properties[ProcessManagerStep.StateKey] = "done";
                return Task.CompletedTask;
            });

        var doneHandler = Substitute.For<IStep>();
        doneHandler.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                visited.Add("done");
                return Task.CompletedTask;
            });

        var sut = new ProcessManagerStep(
            c => c.Properties.TryGetValue(ProcessManagerStep.StateKey, out var sv2) ? (string?)sv2 ?? "init" : "init",
            new Dictionary<string, IStep>
            {
                ["init"] = initHandler,
                ["done"] = doneHandler
            });

        await sut.ExecuteAsync(ctx);

        await Given("states visited during process manager execution", () => visited)
            .Then("init then done were visited", states =>
            {
                states.Should().ContainInOrder("init", "done");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("StateKey constant has expected value"), Fact]
    public async Task StateKeyHasExpectedValue()
    {
        await Given("ProcessManagerStep.StateKey constant", () => ProcessManagerStep.StateKey)
            .Then("value is '__ProcessManagerState'", key =>
            {
                key.Should().Be("__ProcessManagerState");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null stateSelector throws ArgumentNullException"), Fact]
    public async Task NullStateSelectorThrows()
    {
        Exception? caught = null;
        try { _ = new ProcessManagerStep(null!, new Dictionary<string, IStep>()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null state selector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null stateHandlers throws ArgumentNullException"), Fact]
    public async Task NullStateHandlersThrows()
    {
        Exception? caught = null;
        try { _ = new ProcessManagerStep(_ => "x", null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null state handlers", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Context abort stops state machine mid-execution"), Fact]
    public async Task AbortStopsStateMachine()
    {
        var handlerCallCount = 0;
        var ctx = new WorkflowContext();

        var handler = Substitute.For<IStep>();
        handler.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                handlerCallCount++;
                ((IWorkflowContext)ci[0]).IsAborted = true;
                return Task.CompletedTask;
            });

        var sut = new ProcessManagerStep(
            _ => "run",
            new Dictionary<string, IStep> { ["run"] = handler });

        await sut.ExecuteAsync(ctx);

        await Given("handler call count when context is aborted after first run", () => handlerCallCount)
            .Then("handler ran exactly once before abort stopped it", count =>
            {
                count.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }
}
