using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Endpoint;
using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Endpoint;

// Bespoke kept: PollingConsumerStep is a thin EIP endpoint primitive that delegates to
// IPollingSource<T> — a domain interface with no PatternKit equivalent (PatternKit does not
// expose an async polling/pull primitive). Characterization-only coverage locks current
// contract.

[Feature("PollingConsumerStep — characterization (Phase G.4)")]
public class PollingConsumerStepScenarios : TinyBddTestBase
{
    public PollingConsumerStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("PollingConsumerStep.Name returns 'PollingConsumer'"), Fact]
    public async Task NameIsPollingConsumer()
    {
        var source = Substitute.For<IPollingSource<string>>();
        var sut = new PollingConsumerStep<string>(source);

        await Given("PollingConsumerStep instance", () => sut)
            .Then("Name is 'PollingConsumer'", s =>
            {
                s.Name.Should().Be("PollingConsumer");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null source throws ArgumentNullException"), Fact]
    public async Task NullSourceThrows()
    {
        Exception? caught = null;
        try { _ = new PollingConsumerStep<string>(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null source", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ResultKey constant has expected value"), Fact]
    public async Task ResultKeyHasExpectedValue()
    {
        await Given("PollingConsumerStep.ResultKey constant", () => PollingConsumerStep<string>.ResultKey)
            .Then("value is '__PolledItems'", key =>
            {
                key.Should().Be("__PolledItems");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Polled items are stored on the context under ResultKey"), Fact]
    public async Task PolledItemsStoredOnContext()
    {
        var items = new[] { "item-a", "item-b" };
        var source = Substitute.For<IPollingSource<string>>();
        source.PollAsync(Arg.Any<CancellationToken>())
              .Returns(Task.FromResult<IReadOnlyList<string>>(items));

        var ctx = new WorkflowContext();
        var sut = new PollingConsumerStep<string>(source);
        await sut.ExecuteAsync(ctx);

        await Given("context after polling", () => ctx)
            .Then("ResultKey holds the polled items", c =>
            {
                c.Properties[PollingConsumerStep<string>.ResultKey]
                    .Should().BeAssignableTo<IReadOnlyList<string>>()
                    .Which.Should().BeEquivalentTo(items);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty poll result stores an empty list on context"), Fact]
    public async Task EmptyPollStoresEmptyList()
    {
        var source = Substitute.For<IPollingSource<int>>();
        source.PollAsync(Arg.Any<CancellationToken>())
              .Returns(Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>()));

        var ctx = new WorkflowContext();
        var sut = new PollingConsumerStep<int>(source);
        await sut.ExecuteAsync(ctx);

        await Given("context after empty poll", () => ctx)
            .Then("ResultKey holds an empty collection", c =>
            {
                c.Properties[PollingConsumerStep<int>.ResultKey]
                    .Should().BeAssignableTo<IReadOnlyList<int>>()
                    .Which.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("PollAsync is called with the context cancellation token"), Fact]
    public async Task PollAsyncReceivesContextCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;

        var source = Substitute.For<IPollingSource<string>>();
        source.PollAsync(Arg.Do<CancellationToken>(t => capturedToken = t))
              .Returns(Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>()));

        var ctx = new WorkflowContext(cts.Token);
        var sut = new PollingConsumerStep<string>(source);
        await sut.ExecuteAsync(ctx);

        await Given("captured CancellationToken passed to PollAsync", () => capturedToken)
            .Then("it equals the context's CancellationToken", token =>
            {
                token.Should().Be(cts.Token);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Source exception propagates to caller"), Fact]
    public async Task SourceExceptionPropagates()
    {
        var source = Substitute.For<IPollingSource<string>>();
        source.PollAsync(Arg.Any<CancellationToken>())
              .Returns<Task<IReadOnlyList<string>>>(_ => throw new TimeoutException("poll timeout"));

        Exception? caught = null;
        var sut = new PollingConsumerStep<string>(source);
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (TimeoutException ex) { caught = ex; }

        await Given("exception from PollAsync", () => caught)
            .Then("TimeoutException propagates to caller", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Be("poll timeout");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Subsequent polls replace the previous ResultKey value"), Fact]
    public async Task SubsequentPollsOverwriteResultKey()
    {
        var firstItems = new[] { "first" };
        var secondItems = new[] { "second-a", "second-b" };
        var callCount = 0;

        var source = Substitute.For<IPollingSource<string>>();
        source.PollAsync(Arg.Any<CancellationToken>())
              .Returns(_ => Task.FromResult<IReadOnlyList<string>>(
                  ++callCount == 1 ? firstItems : secondItems));

        var ctx = new WorkflowContext();
        var sut = new PollingConsumerStep<string>(source);
        await sut.ExecuteAsync(ctx); // first poll
        await sut.ExecuteAsync(ctx); // second poll

        await Given("context after two consecutive polls", () => ctx)
            .Then("ResultKey contains the second poll's items", c =>
            {
                c.Properties[PollingConsumerStep<string>.ResultKey]
                    .Should().BeAssignableTo<IReadOnlyList<string>>()
                    .Which.Should().BeEquivalentTo(secondItems);
                return true;
            })
            .AssertPassed();
    }
}
