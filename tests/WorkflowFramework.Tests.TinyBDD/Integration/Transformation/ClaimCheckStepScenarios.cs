using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Transformation;
using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Transformation;

// PatternKit Flyweight/Proxy were evaluated for ClaimCheckStep — neither fits.
// Flyweight is about sharing instances; Proxy is about access control to a single
// object. ClaimCheckStep + ClaimRetrieveStep form a store/retrieve EIP pattern
// with external state. Bespoke kept; characterization-only coverage provided.

[Feature("ClaimCheckStep & ClaimRetrieveStep — characterization (Phase G.5)")]
public class ClaimCheckStepScenarios : TinyBddTestBase
{
    public ClaimCheckStepScenarios(ITestOutputHelper output) : base(output) { }

    // ── ClaimCheckStep ──────────────────────────────────────────────────────

    [Scenario("ClaimCheckStep.Name returns 'ClaimCheck'"), Fact]
    public async Task ClaimCheckNameIsClaimCheck()
    {
        var store = Substitute.For<IClaimCheckStore>();
        var sut = new ClaimCheckStep(store, _ => new object());

        await Given("ClaimCheckStep instance", () => sut)
            .Then("Name is 'ClaimCheck'", s =>
            {
                s.Name.Should().Be("ClaimCheck");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ClaimCheckStep null store throws ArgumentNullException"), Fact]
    public async Task ClaimCheckNullStoreThrows()
    {
        Exception? caught = null;
        try { _ = new ClaimCheckStep(null!, _ => new object()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null store", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ClaimCheckStep null payloadSelector throws ArgumentNullException"), Fact]
    public async Task ClaimCheckNullPayloadSelectorThrows()
    {
        var store = Substitute.For<IClaimCheckStore>();
        Exception? caught = null;
        try { _ = new ClaimCheckStep(store, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null payloadSelector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ClaimTicketKey constant has expected value"), Fact]
    public async Task ClaimTicketKeyHasExpectedValue()
    {
        await Given("ClaimCheckStep.ClaimTicketKey constant", () => ClaimCheckStep.ClaimTicketKey)
            .Then("value is '__ClaimTicket'", key =>
            {
                key.Should().Be("__ClaimTicket");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync stores the returned ticket on context"), Fact]
    public async Task ExecuteStoresTicketOnContext()
    {
        var store = Substitute.For<IClaimCheckStore>();
        store.StoreAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
             .Returns("ticket-xyz");

        var ctx = new WorkflowContext();
        var sut = new ClaimCheckStep(store, _ => new { Data = "large-payload" });
        await sut.ExecuteAsync(ctx);

        await Given("context after claim check step", () => ctx)
            .Then("ClaimTicketKey holds the ticket returned by the store", c =>
            {
                c.Properties[ClaimCheckStep.ClaimTicketKey].Should().Be("ticket-xyz");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("StoreAsync is called with the payload selected from context"), Fact]
    public async Task StoreAsyncReceivesSelectedPayload()
    {
        object? savedPayload = null;
        var store = Substitute.For<IClaimCheckStore>();
        store.StoreAsync(Arg.Do<object>(p => savedPayload = p), Arg.Any<CancellationToken>())
             .Returns("t");

        var payload = new { Value = 99 };
        var ctx = new WorkflowContext();
        ctx.Properties["payload"] = payload;
        var sut = new ClaimCheckStep(store, c => c.Properties["payload"]!);
        await sut.ExecuteAsync(ctx);

        await Given("payload passed to StoreAsync", () => savedPayload)
            .Then("it is the payload selected from context", p =>
            {
                p.Should().BeSameAs(payload);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("StoreAsync is called with the context cancellation token"), Fact]
    public async Task StoreAsyncReceivesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;

        var store = Substitute.For<IClaimCheckStore>();
        store.StoreAsync(
                Arg.Any<object>(),
                Arg.Do<CancellationToken>(t => capturedToken = t))
             .Returns("t");

        var ctx = new WorkflowContext(cts.Token);
        var sut = new ClaimCheckStep(store, _ => "payload");
        await sut.ExecuteAsync(ctx);

        await Given("captured CancellationToken from StoreAsync", () => capturedToken)
            .Then("it equals the context's CancellationToken", token =>
            {
                token.Should().Be(cts.Token);
                return true;
            })
            .AssertPassed();
    }

    // ── ClaimRetrieveStep ───────────────────────────────────────────────────

    [Scenario("ClaimRetrieveStep.Name returns 'ClaimRetrieve'"), Fact]
    public async Task ClaimRetrieveNameIsClaimRetrieve()
    {
        var store = Substitute.For<IClaimCheckStore>();
        var sut = new ClaimRetrieveStep(store);

        await Given("ClaimRetrieveStep instance", () => sut)
            .Then("Name is 'ClaimRetrieve'", s =>
            {
                s.Name.Should().Be("ClaimRetrieve");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ClaimRetrieveStep null store throws ArgumentNullException"), Fact]
    public async Task ClaimRetrieveNullStoreThrows()
    {
        Exception? caught = null;
        try { _ = new ClaimRetrieveStep(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null store", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ClaimRetrieveStep retrieves payload using ticket from context"), Fact]
    public async Task ClaimRetrieveUsesTicketFromContext()
    {
        var payload = new { Retrieved = true };
        var store = Substitute.For<IClaimCheckStore>();
        store.RetrieveAsync("ticket-abc", Arg.Any<CancellationToken>())
             .Returns<object>(payload);

        var ctx = new WorkflowContext();
        ctx.Properties[ClaimCheckStep.ClaimTicketKey] = "ticket-abc";
        var sut = new ClaimRetrieveStep(store);
        await sut.ExecuteAsync(ctx);

        await Given("context after retrieve step", () => ctx)
            .Then("default result key holds the retrieved payload", c =>
            {
                c.Properties["__ClaimPayload"].Should().BeSameAs(payload);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ClaimRetrieveStep with custom resultKey stores payload under that key"), Fact]
    public async Task ClaimRetrieveCustomResultKeyUsed()
    {
        var payload = "the-payload";
        var store = Substitute.For<IClaimCheckStore>();
        store.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns<object>(payload);

        var ctx = new WorkflowContext();
        ctx.Properties[ClaimCheckStep.ClaimTicketKey] = "t";
        var sut = new ClaimRetrieveStep(store, "myResult");
        await sut.ExecuteAsync(ctx);

        await Given("context after retrieve with custom key 'myResult'", () => ctx)
            .Then("payload is under 'myResult'", c =>
            {
                c.Properties["myResult"].Should().Be(payload);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ClaimRetrieveStep throws when no ticket on context"), Fact]
    public async Task ClaimRetrieveNoTicketThrows()
    {
        // Characterization: when ClaimTicketKey is missing from context.Properties the
        // dictionary indexer throws KeyNotFoundException (not InvalidOperationException).
        // InvalidOperationException is only thrown when the key exists but casts to null.
        var store = Substitute.For<IClaimCheckStore>();
        var sut = new ClaimRetrieveStep(store);
        var ctx = new WorkflowContext(); // no ticket set

        Exception? caught = null;
        try { await sut.ExecuteAsync(ctx); }
        catch (Exception ex) { caught = ex; }

        await Given("exception when ClaimTicketKey is absent from context", () => caught)
            .Then("an exception is thrown (KeyNotFoundException from missing dictionary key)", ex =>
            {
                ex.Should().NotBeNull();
                ex.Should().BeOfType<KeyNotFoundException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ClaimRetrieveStep throws InvalidOperationException when ticket key exists but is null"), Fact]
    public async Task ClaimRetrieveNullTicketThrows()
    {
        var store = Substitute.For<IClaimCheckStore>();
        var sut = new ClaimRetrieveStep(store);
        var ctx = new WorkflowContext();
        ctx.Properties[ClaimCheckStep.ClaimTicketKey] = null!; // key present but null

        Exception? caught = null;
        try { await sut.ExecuteAsync(ctx); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception when ClaimTicketKey is null", () => caught)
            .Then("InvalidOperationException mentions running ClaimCheckStep first", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Contain("ClaimCheckStep");
                return true;
            })
            .AssertPassed();
    }
}
