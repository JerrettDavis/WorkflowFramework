using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Endpoint;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Endpoint;

// Iteration 2: IdempotentReceiverStep now delegates to PatternKit IIdempotencyStore with
// claim → invoke → complete/fail semantics. The prior bespoke HashSet.Add-before-invoke
// behaviour permanently locked the key on failure (latent bug). PatternKit's model correctly
// allows retry after a failed attempt. See .plan/patternkit-iteration-2.md §4.

[Feature("IdempotentReceiverStep — characterization (Phase G.4 / Iter2)")]
public class IdempotentReceiverStepScenarios : TinyBddTestBase
{
    public IdempotentReceiverStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("IdempotentReceiverStep.Name returns 'IdempotentReceiver'"), Fact]
    public async Task NameIsIdempotentReceiver()
    {
        var inner = Substitute.For<IStep>();
        var sut = new IdempotentReceiverStep(inner, _ => "id");

        await Given("IdempotentReceiverStep instance", () => sut)
            .Then("Name property is 'IdempotentReceiver'", s =>
            {
                s.Name.Should().Be("IdempotentReceiver");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null innerStep throws ArgumentNullException"), Fact]
    public async Task NullInnerStepThrows()
    {
        Exception? caught = null;
        try { _ = new IdempotentReceiverStep(null!, _ => "id"); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null innerStep", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null messageIdSelector throws ArgumentNullException"), Fact]
    public async Task NullMessageIdSelectorThrows()
    {
        var inner = Substitute.For<IStep>();
        Exception? caught = null;
        try { _ = new IdempotentReceiverStep(inner, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null messageIdSelector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("First call with a new message ID delegates to inner step"), Fact]
    public async Task FirstCallDelegatesToInner()
    {
        var callCount = 0;
        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { callCount++; return Task.CompletedTask; });

        var ctx = new WorkflowContext();
        ctx.Properties["msgId"] = "abc";
        var sut = new IdempotentReceiverStep(inner, c => (string)c.Properties["msgId"]);
        await sut.ExecuteAsync(ctx);

        await Given("call count after first execution with new ID", () => callCount)
            .Then("inner step was called once", count =>
            {
                count.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Second call with the same message ID is silently skipped"), Fact]
    public async Task DuplicateMessageIsSkipped()
    {
        var callCount = 0;
        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { callCount++; return Task.CompletedTask; });

        var sut = new IdempotentReceiverStep(inner, _ => "dup-id");
        var ctx = new WorkflowContext();

        await sut.ExecuteAsync(ctx);
        await sut.ExecuteAsync(ctx); // duplicate

        await Given("call count after two executions with the same ID", () => callCount)
            .Then("inner step was called only once", count =>
            {
                count.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Different message IDs are each processed independently"), Fact]
    public async Task DifferentIdsAreEachProcessed()
    {
        var callCount = 0;
        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { callCount++; return Task.CompletedTask; });

        var ids = new[] { "id-1", "id-2", "id-3" };
        var idx = 0;
        var sut = new IdempotentReceiverStep(inner, _ => ids[idx++]);
        var ctx = new WorkflowContext();

        await sut.ExecuteAsync(ctx);
        await sut.ExecuteAsync(ctx);
        await sut.ExecuteAsync(ctx);

        await Given("call count after three executions with distinct IDs", () => callCount)
            .Then("inner step was called three times", count =>
            {
                count.Should().Be(3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Seen IDs accumulate across multiple unique messages"), Fact]
    public async Task SeenIdsAccumulate()
    {
        var processed = new List<string>();
        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                processed.Add((string)ci.Arg<IWorkflowContext>().Properties["last"]);
                return Task.CompletedTask;
            });

        var sut = new IdempotentReceiverStep(inner, c => (string)c.Properties["last"]);

        foreach (var id in new[] { "a", "b", "a", "c", "b" })
        {
            var ctx = new WorkflowContext();
            ctx.Properties["last"] = id;
            await sut.ExecuteAsync(ctx);
        }

        await Given("processed IDs after mixed sequence", () => processed)
            .Then("only the three distinct IDs were processed", list =>
            {
                list.Should().BeEquivalentTo(new[] { "a", "b", "c" });
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Inner step exception propagates to caller"), Fact]
    public async Task InnerExceptionPropagatesToCaller()
    {
        var callCount = 0;
        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns<Task>(_ =>
            {
                callCount++;
                throw new InvalidOperationException("inner boom");
            });

        var sut = new IdempotentReceiverStep(inner, _ => "err-id");
        var ctx = new WorkflowContext();

        Exception? caught = null;
        try { await sut.ExecuteAsync(ctx); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception from inner step on first call", () => caught)
            .Then("InvalidOperationException propagates to caller", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Be("inner boom");
                return true;
            })
            .AssertPassed();
    }

    // This is a deliberate bug fix in Iteration 2. Prior behavior was permanent dedup on failure
    // (latent bug — failure should not lock future attempts). PatternKit's IIdempotencyStore
    // provides claim/complete/fail semantics; the default RetryAfterFailureIdempotencyStore
    // resets a Failed key to Processing on the next TryClaim, correctly allowing retry.
    // See .plan/patternkit-iteration-2.md §4.
    [Scenario("Re-attempt after exception is allowed (failure releases the idempotency key)"), Fact]
    public async Task ReAttemptAfterExceptionIsAllowed()
    {
        var callCount = 0;
        var inner = Substitute.For<IStep>();
        inner.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns<Task>(_ =>
            {
                callCount++;
                throw new InvalidOperationException("boom");
            });

        var sut = new IdempotentReceiverStep(inner, _ => "retry-id");
        var ctx = new WorkflowContext();

        try { await sut.ExecuteAsync(ctx); } catch { /* expected — first attempt fails */ }
        try { await sut.ExecuteAsync(ctx); } catch { /* second attempt — now allowed */ }

        await Given("call count after first-attempt failure then second attempt", () => callCount)
            .Then("inner was called twice (failure released the key, allowing retry)", count =>
            {
                count.Should().Be(2);
                return true;
            })
            .AssertPassed();
    }
}
