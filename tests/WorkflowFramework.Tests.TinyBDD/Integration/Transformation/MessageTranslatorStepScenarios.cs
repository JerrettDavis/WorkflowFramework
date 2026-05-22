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

// PatternKit AsyncAdapter<TIn,TOut> was evaluated for MessageTranslatorStep<TIn,TOut>.
// The step delegates to IMessageTranslator<TIn,TOut>.TranslateAsync — a single async
// mapping step — which could be expressed as AsyncAdapter.Create(seedFrom).Build().
// However: (1) IMessageTranslator is a public interface contract callers already depend
// on; wrapping it behind AsyncAdapter would add a layer with no observable benefit, and
// (2) the step's context-property side effect (storing the output under _outputKey) has
// no natural place in AsyncAdapter's pipeline. Bespoke kept; rationale documented here.
// Characterization-only coverage locks the current contract.

[Feature("MessageTranslatorStep — characterization (Phase G.5)")]
public class MessageTranslatorStepScenarios : TinyBddTestBase
{
    public MessageTranslatorStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("MessageTranslatorStep.Name returns 'MessageTranslator'"), Fact]
    public async Task NameIsMessageTranslator()
    {
        var translator = Substitute.For<IMessageTranslator<string, int>>();
        var sut = new MessageTranslatorStep<string, int>(translator, _ => "input");

        await Given("MessageTranslatorStep instance", () => sut)
            .Then("Name is 'MessageTranslator'", s =>
            {
                s.Name.Should().Be("MessageTranslator");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null translator throws ArgumentNullException"), Fact]
    public async Task NullTranslatorThrows()
    {
        Exception? caught = null;
        try { _ = new MessageTranslatorStep<string, int>(null!, _ => "x"); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null translator", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null inputSelector throws ArgumentNullException"), Fact]
    public async Task NullInputSelectorThrows()
    {
        var translator = Substitute.For<IMessageTranslator<string, int>>();
        Exception? caught = null;
        try { _ = new MessageTranslatorStep<string, int>(translator, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null inputSelector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Default outputKey is '__TranslatedOutput'"), Fact]
    public async Task DefaultOutputKeyIsTranslatedOutput()
    {
        var translator = Substitute.For<IMessageTranslator<string, int>>();
        translator.TranslateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(42);

        var ctx = new WorkflowContext();
        ctx.Properties["msg"] = "hello";
        var sut = new MessageTranslatorStep<string, int>(translator, c => (string)c.Properties["msg"]);
        await sut.ExecuteAsync(ctx);

        await Given("context after translation with default key", () => ctx)
            .Then("output is stored under '__TranslatedOutput'", c =>
            {
                c.Properties.Should().ContainKey("__TranslatedOutput");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Custom outputKey stores translated result under that key"), Fact]
    public async Task CustomOutputKeyUsed()
    {
        var translator = Substitute.For<IMessageTranslator<string, string>>();
        translator.TranslateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns("translated");

        var ctx = new WorkflowContext();
        var sut = new MessageTranslatorStep<string, string>(translator, _ => "raw", "myKey");
        await sut.ExecuteAsync(ctx);

        await Given("context after translation with custom key 'myKey'", () => ctx)
            .Then("output is stored under 'myKey'", c =>
            {
                c.Properties["myKey"].Should().Be("translated");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TranslateAsync receives the value extracted by inputSelector"), Fact]
    public async Task InputSelectorValuePassedToTranslator()
    {
        string? capturedInput = null;
        var translator = Substitute.For<IMessageTranslator<string, string>>();
        translator.TranslateAsync(
                Arg.Do<string>(s => capturedInput = s),
                Arg.Any<CancellationToken>())
            .Returns("out");

        var ctx = new WorkflowContext();
        ctx.Properties["raw"] = "hello-world";
        var sut = new MessageTranslatorStep<string, string>(
            translator,
            c => (string)c.Properties["raw"]);
        await sut.ExecuteAsync(ctx);

        await Given("input value captured by translator", () => capturedInput)
            .Then("it equals the value selected from context", v =>
            {
                v.Should().Be("hello-world");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TranslateAsync is called with the context cancellation token"), Fact]
    public async Task CancellationTokenForwardedToTranslator()
    {
        using var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;

        var translator = Substitute.For<IMessageTranslator<string, string>>();
        translator.TranslateAsync(
                Arg.Any<string>(),
                Arg.Do<CancellationToken>(t => capturedToken = t))
            .Returns("out");

        var ctx = new WorkflowContext(cts.Token);
        var sut = new MessageTranslatorStep<string, string>(translator, _ => "input");
        await sut.ExecuteAsync(ctx);

        await Given("captured CancellationToken in TranslateAsync", () => capturedToken)
            .Then("it equals the context's CancellationToken", token =>
            {
                token.Should().Be(cts.Token);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Translator exception propagates to caller"), Fact]
    public async Task TranslatorExceptionPropagates()
    {
        var translator = Substitute.For<IMessageTranslator<string, int>>();
        translator.TranslateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns<Task<int>>(_ => throw new InvalidOperationException("translation failed"));

        Exception? caught = null;
        var sut = new MessageTranslatorStep<string, int>(translator, _ => "x");
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception from TranslateAsync", () => caught)
            .Then("InvalidOperationException propagates", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Be("translation failed");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Translated output value is correctly stored in context"), Fact]
    public async Task TranslatedValueStoredInContext()
    {
        var translator = Substitute.For<IMessageTranslator<int, string>>();
        translator.TranslateAsync(42, Arg.Any<CancellationToken>())
                  .Returns("forty-two");

        var ctx = new WorkflowContext();
        var sut = new MessageTranslatorStep<int, string>(translator, _ => 42, "result");
        await sut.ExecuteAsync(ctx);

        await Given("context after translating 42 → 'forty-two'", () => ctx)
            .Then("result key holds 'forty-two'", c =>
            {
                c.Properties["result"].Should().Be("forty-two");
                return true;
            })
            .AssertPassed();
    }
}
