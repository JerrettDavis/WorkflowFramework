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

[Feature("MessageBridgeStep — characterization (Phase G.3)")]
public class MessageBridgeStepScenarios : TinyBddTestBase
{
    public MessageBridgeStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("MessageBridgeStep Name returns 'MessageBridge'"), Fact]
    public async Task NameIsMessageBridge()
    {
        var source = Substitute.For<IChannelAdapter>();
        var dest = Substitute.For<IChannelAdapter>();
        var sut = new MessageBridgeStep(source, dest);

        await Given("MessageBridgeStep instance", () => sut)
            .Then("Name is 'MessageBridge'", s =>
            {
                s.Name.Should().Be("MessageBridge");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Message received from source is forwarded to destination"), Fact]
    public async Task MessageForwardedFromSourceToDestination()
    {
        var payload = new object();
        object? received = null;

        var source = Substitute.For<IChannelAdapter>();
        source.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(payload));

        var dest = Substitute.For<IChannelAdapter>();
        dest.SendAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ci => { received = ci[0]; return Task.CompletedTask; });

        var sut = new MessageBridgeStep(source, dest);
        await sut.ExecuteAsync(new WorkflowContext());

        await Given("message received by destination", () => received)
            .Then("destination received the source message", msg =>
            {
                msg.Should().BeSameAs(payload);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null message from source — destination SendAsync is not called"), Fact]
    public async Task NullMessageFromSourceSkipsSend()
    {
        var source = Substitute.For<IChannelAdapter>();
        source.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<object?>(null));

        var dest = Substitute.For<IChannelAdapter>();

        var sut = new MessageBridgeStep(source, dest);
        await sut.ExecuteAsync(new WorkflowContext());

        await dest.DidNotReceive().SendAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());

        await Given("destination not called when source returns null", () => true)
            .Then("destination SendAsync was NOT called (verified by NSubstitute)", _ => true)
            .AssertPassed();
    }

    [Scenario("Null source throws ArgumentNullException"), Fact]
    public async Task NullSourceThrows()
    {
        var dest = Substitute.For<IChannelAdapter>();
        Exception? caught = null;
        try { _ = new MessageBridgeStep(null!, dest); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null source", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null destination throws ArgumentNullException"), Fact]
    public async Task NullDestinationThrows()
    {
        var source = Substitute.For<IChannelAdapter>();
        Exception? caught = null;
        try { _ = new MessageBridgeStep(source, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null destination", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Context cancellation token is passed to source ReceiveAsync"), Fact]
    public async Task CancellationTokenPassedToSourceReceive()
    {
        var cts = new CancellationTokenSource();
        CancellationToken receivedToken = default;

        var source = Substitute.For<IChannelAdapter>();
        source.ReceiveAsync(Arg.Any<CancellationToken>())
            .Returns(ci => { receivedToken = (CancellationToken)ci[0]; return Task.FromResult<object?>(null); });

        var dest = Substitute.For<IChannelAdapter>();
        var ctx = new WorkflowContext(cts.Token);

        var sut = new MessageBridgeStep(source, dest);
        await sut.ExecuteAsync(ctx);

        await Given("cancellation token passed to source ReceiveAsync", () => receivedToken)
            .Then("token equals context cancellation token", t =>
            {
                t.Should().Be(cts.Token);
                return true;
            })
            .AssertPassed();
    }
}
