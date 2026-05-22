using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Transformation;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Transformation;

// Bespoke kept: NormalizerStep is a format-dispatch table (dictionary of string → IStep).
// PatternKit Strategy<TContext,TResult> requires a typed discriminator enum/class, not an
// open string key. Adapting would mean wrapping the dictionary lookup with a Strategy
// factory, adding indirection without observable benefit. Characterization-only coverage
// locks the current contract.

[Feature("NormalizerStep — characterization (Phase G.5)")]
public class NormalizerStepScenarios : TinyBddTestBase
{
    public NormalizerStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("NormalizerStep.Name returns 'Normalizer'"), Fact]
    public async Task NameIsNormalizer()
    {
        var sut = new NormalizerStep(_ => "fmt", new Dictionary<string, IStep>());

        await Given("NormalizerStep instance", () => sut)
            .Then("Name is 'Normalizer'", s =>
            {
                s.Name.Should().Be("Normalizer");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null formatDetector throws ArgumentNullException"), Fact]
    public async Task NullFormatDetectorThrows()
    {
        Exception? caught = null;
        try { _ = new NormalizerStep(null!, new Dictionary<string, IStep>()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null formatDetector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null translators dictionary throws ArgumentNullException"), Fact]
    public async Task NullTranslatorsDictionaryThrows()
    {
        Exception? caught = null;
        try { _ = new NormalizerStep(_ => "fmt", null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null translators", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Known format delegates to the matching translator"), Fact]
    public async Task KnownFormatDelegatesToTranslator()
    {
        var called = false;
        var xmlStep = Substitute.For<IStep>();
        xmlStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
               .Returns(_ => { called = true; return Task.CompletedTask; });

        var sut = new NormalizerStep(
            _ => "xml",
            new Dictionary<string, IStep> { ["xml"] = xmlStep });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("called flag after executing normalizer with 'xml' format", () => called)
            .Then("the xml translator was invoked", c =>
            {
                c.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Unknown format with no default throws InvalidOperationException"), Fact]
    public async Task UnknownFormatNoDefaultThrows()
    {
        var sut = new NormalizerStep(
            _ => "unknown-format",
            new Dictionary<string, IStep>());

        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception when format is unknown and no default translator", () => caught)
            .Then("InvalidOperationException mentions the unknown format", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Contain("unknown-format");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Unknown format with a default translator uses the default"), Fact]
    public async Task UnknownFormatUsesDefault()
    {
        var defaultCalled = false;
        var defaultStep = Substitute.For<IStep>();
        defaultStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
                   .Returns(_ => { defaultCalled = true; return Task.CompletedTask; });

        var sut = new NormalizerStep(
            _ => "mystery",
            new Dictionary<string, IStep>(),
            defaultStep);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("defaultCalled flag after executing normalizer with unknown format", () => defaultCalled)
            .Then("the default translator was invoked", c =>
            {
                c.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Only the matching translator is invoked, not others"), Fact]
    public async Task OnlyMatchingTranslatorInvoked()
    {
        var jsonCalled = false;
        var csvCalled = false;

        var jsonStep = Substitute.For<IStep>();
        jsonStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
                .Returns(_ => { jsonCalled = true; return Task.CompletedTask; });

        var csvStep = Substitute.For<IStep>();
        csvStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
               .Returns(_ => { csvCalled = true; return Task.CompletedTask; });

        var sut = new NormalizerStep(
            _ => "json",
            new Dictionary<string, IStep> { ["json"] = jsonStep, ["csv"] = csvStep });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("invocation flags after routing to 'json'", () => (jsonCalled, csvCalled))
            .Then("json was called and csv was not", flags =>
            {
                flags.jsonCalled.Should().BeTrue();
                flags.csvCalled.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Format detector receives the workflow context"), Fact]
    public async Task FormatDetectorReceivesContext()
    {
        IWorkflowContext? captured = null;
        var ctx = new WorkflowContext();
        ctx.Properties["format"] = "xml";

        var xmlStep = Substitute.For<IStep>();
        xmlStep.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(Task.CompletedTask);

        var sut = new NormalizerStep(
            c => { captured = c; return (string)c.Properties["format"]; },
            new Dictionary<string, IStep> { ["xml"] = xmlStep });

        await sut.ExecuteAsync(ctx);

        await Given("context captured by formatDetector", () => captured)
            .Then("it is the same context passed to ExecuteAsync", c =>
            {
                c.Should().BeSameAs(ctx);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Translator exception propagates to caller"), Fact]
    public async Task TranslatorExceptionPropagates()
    {
        var faultyStep = Substitute.For<IStep>();
        faultyStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
                  .Returns<Task>(_ => throw new DataMisalignedException("bad data"));

        var sut = new NormalizerStep(
            _ => "bad",
            new Dictionary<string, IStep> { ["bad"] = faultyStep });

        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (DataMisalignedException ex) { caught = ex; }

        await Given("exception from translator step", () => caught)
            .Then("DataMisalignedException propagates", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Be("bad data");
                return true;
            })
            .AssertPassed();
    }
}
