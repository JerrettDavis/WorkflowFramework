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

[Feature("ChannelAdapterStep — characterization (Phase G.3)")]
public class ChannelAdapterStepScenarios : TinyBddTestBase
{
    public ChannelAdapterStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("ChannelAdapterStep Name returns 'ChannelAdapter'"), Fact]
    public async Task NameIsChannelAdapter()
    {
        var adapter = Substitute.For<IChannelAdapter>();
        var sut = new ChannelAdapterStep(adapter, _ => new object());

        await Given("ChannelAdapterStep instance", () => sut)
            .Then("Name is 'ChannelAdapter'", s =>
            {
                s.Name.Should().Be("ChannelAdapter");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Adapter SendAsync is called with message from selector"), Fact]
    public async Task AdapterSendAsyncCalledWithSelectedMessage()
    {
        object? sentMessage = null;
        var adapter = Substitute.For<IChannelAdapter>();
        adapter.SendAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ci => { sentMessage = ci[0]; return Task.CompletedTask; });

        var ctx = new WorkflowContext();
        ctx.Properties["payload"] = "hello";

        var sut = new ChannelAdapterStep(
            adapter,
            c => c.Properties["payload"]!);

        await sut.ExecuteAsync(ctx);

        await Given("message sent to adapter", () => sentMessage)
            .Then("sent message equals 'hello'", msg =>
            {
                msg.Should().Be("hello");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null adapter throws ArgumentNullException"), Fact]
    public async Task NullAdapterThrows()
    {
        Exception? caught = null;
        try { _ = new ChannelAdapterStep(null!, _ => new object()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null adapter", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null messageSelector throws ArgumentNullException"), Fact]
    public async Task NullMessageSelectorThrows()
    {
        var adapter = Substitute.For<IChannelAdapter>();
        Exception? caught = null;
        try { _ = new ChannelAdapterStep(adapter, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null message selector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Adapter SendAsync receives context cancellation token"), Fact]
    public async Task SendAsyncReceivesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        CancellationToken received = default;
        var adapter = Substitute.For<IChannelAdapter>();
        adapter.SendAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ci => { received = (CancellationToken)ci[1]; return Task.CompletedTask; });

        var ctx = new WorkflowContext(cts.Token);
        var sut = new ChannelAdapterStep(adapter, _ => "msg");

        await sut.ExecuteAsync(ctx);

        await Given("cancellation token received by SendAsync", () => received)
            .Then("token equals the context cancellation token", t =>
            {
                t.Should().Be(cts.Token);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Message selector can return complex objects"), Fact]
    public async Task MessageSelectorReturnsComplexObject()
    {
        object? sentMessage = null;
        var adapter = Substitute.For<IChannelAdapter>();
        adapter.SendAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ci => { sentMessage = ci[0]; return Task.CompletedTask; });

        var complexPayload = new { Id = 42, Name = "Test" };
        var ctx = new WorkflowContext();
        ctx.Properties["complex"] = complexPayload;

        var sut = new ChannelAdapterStep(adapter, c => c.Properties["complex"]!);
        await sut.ExecuteAsync(ctx);

        await Given("message sent to adapter for complex object", () => sentMessage)
            .Then("sent message is the complex payload", msg =>
            {
                msg.Should().BeSameAs(complexPayload);
                return true;
            })
            .AssertPassed();
    }
}
