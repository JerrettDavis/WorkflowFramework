using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Channel;
using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Channel;

[Feature("DeadLetterStep — characterization (Phase G.3)")]
public class DeadLetterStepScenarios : TinyBddTestBase
{
    public DeadLetterStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("DeadLetterStep Name returns 'DeadLetter'"), Fact]
    public async Task NameIsDeadLetter()
    {
        var store = Substitute.For<IDeadLetterStore>();
        var inner = Substitute.For<IStep>();
        var sut = new DeadLetterStep(store, inner);

        await Given("DeadLetterStep instance", () => sut)
            .Then("Name is 'DeadLetter'", s =>
            {
                s.Name.Should().Be("DeadLetter");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Inner step succeeds — dead letter store is not called"), Fact]
    public async Task SuccessfulInnerStepDoesNotCallStore()
    {
        var store = Substitute.For<IDeadLetterStore>();
        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(Task.CompletedTask);

        var sut = new DeadLetterStep(store, inner);
        await sut.ExecuteAsync(new WorkflowContext());

        await store.DidNotReceive().SendAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>());

        await Given("store not called on success", () => true)
            .Then("store was not called (verified by NSubstitute)", _ => true)
            .AssertPassed();
    }

    [Scenario("Inner step fails — dead letter store SendAsync is called"), Fact]
    public async Task FailingInnerStepCallsStore()
    {
        var store = Substitute.For<IDeadLetterStore>();
        store.SendAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns<Task>(_ => throw new InvalidOperationException("inner failure"));

        var sut = new DeadLetterStep(store, inner);
        await sut.ExecuteAsync(new WorkflowContext());

        await store.Received(1).SendAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>());

        await Given("store called when inner step throws", () => true)
            .Then("store.SendAsync was called once (verified by NSubstitute)", _ => true)
            .AssertPassed();
    }

    [Scenario("Exception message is passed to store SendAsync"), Fact]
    public async Task ExceptionMessagePassedToStore()
    {
        string? storedReason = null;
        var store = Substitute.For<IDeadLetterStore>();
        store.SendAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(ci => { storedReason = (string)ci[1]; return Task.CompletedTask; });

        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns<Task>(_ => throw new Exception("specific error message"));

        var sut = new DeadLetterStep(store, inner);
        await sut.ExecuteAsync(new WorkflowContext());

        await Given("reason passed to dead letter store", () => storedReason)
            .Then("reason contains the exception message", reason =>
            {
                reason.Should().Contain("specific error message");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("__CurrentMessage context property is used as dead letter message"), Fact]
    public async Task CurrentMessagePropertyUsedAsDeadLetterMessage()
    {
        object? storedMessage = null;
        var store = Substitute.For<IDeadLetterStore>();
        store.SendAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(ci => { storedMessage = ci[0]; return Task.CompletedTask; });

        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns<Task>(_ => throw new Exception("fail"));

        var ctx = new WorkflowContext();
        var expectedMessage = new { Id = 7 };
        ctx.Properties["__CurrentMessage"] = expectedMessage;

        var sut = new DeadLetterStep(store, inner);
        await sut.ExecuteAsync(ctx);

        await Given("message stored in dead letter", () => storedMessage)
            .Then("message is the __CurrentMessage property value", msg =>
            {
                msg.Should().BeSameAs(expectedMessage);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Context is used as message when __CurrentMessage is absent"), Fact]
    public async Task ContextUsedAsMessageWhenNoCurrentMessage()
    {
        object? storedMessage = null;
        var store = Substitute.For<IDeadLetterStore>();
        store.SendAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(ci => { storedMessage = ci[0]; return Task.CompletedTask; });

        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns<Task>(_ => throw new Exception("fail"));

        var ctx = new WorkflowContext(); // No __CurrentMessage
        var sut = new DeadLetterStep(store, inner);
        await sut.ExecuteAsync(ctx);

        await Given("message stored when __CurrentMessage absent", () => storedMessage)
            .Then("stored message is the workflow context itself", msg =>
            {
                msg.Should().BeSameAs(ctx);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null store throws ArgumentNullException"), Fact]
    public async Task NullStoreThrows()
    {
        var inner = Substitute.For<IStep>();
        Exception? caught = null;
        try { _ = new DeadLetterStep(null!, inner); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null store", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null inner step throws ArgumentNullException"), Fact]
    public async Task NullInnerStepThrows()
    {
        var store = Substitute.For<IDeadLetterStore>();
        Exception? caught = null;
        try { _ = new DeadLetterStep(store, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null inner step", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }
}
